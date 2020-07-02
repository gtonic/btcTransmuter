using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BtcTransmuter.Abstractions.Extensions;
using BtcTransmuter.Abstractions.ExternalServices;
using BtcTransmuter.Abstractions.Recipes;
using BtcTransmuter.Data.Entities;
using BtcTransmuter.Data.Models;
using BtcTransmuter.Extension.Exchange.Actions.PlaceOrder;
using BtcTransmuter.Extension.Exchange.ExternalServices.Exchange;
using BtcTransmuter.Extension.Timer.Triggers.Timer;
using ExchangeSharp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;

namespace BtcTransmuter.Extension.Presets
{
    [Route("presets-plugin/presets/acd")]
    [Authorize]
    public class ACDController : Controller, ITransmuterPreset
    {
        private readonly IExternalServiceManager _externalServiceManager;
        private readonly UserManager<User> _userManager;
        private readonly IRecipeManager _recipeManager;
        public string Id { get; } = "ACD";
        public string Name { get; } = "Average Cost Dollar (Sell)";
        public string Description { get; } = "Schedule daily sell of assets";

        public ACDController(
            IExternalServiceManager externalServiceManager,
            UserManager<User> userManager,
            IRecipeManager recipeManager)
        {
            _externalServiceManager = externalServiceManager;
            _userManager = userManager;
            _recipeManager = recipeManager;
        }

        public (string ControllerName, string ActionName) GetLink()
        {
            return (Id, nameof(Create));
        }

        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var services = await GetServices();

            return View(new CreateACDViewModel()
            {
                ExchangeServices = new SelectList(services, nameof(ExternalServiceData.Id), nameof(ExternalServiceData.Name))
            });
        }

        private async Task<IEnumerable<ExternalServiceData>> GetServices()
        {
            var services = await _externalServiceManager.GetExternalServicesData(new ExternalServicesDataQuery()
            {
                UserId = _userManager.GetUserId(User),
                Type = new[] {Exchange.ExternalServices.Exchange.ExchangeService.ExchangeServiceType}
            });
            var exchangeServices = services.Where(data => data.Type == Exchange.ExternalServices.Exchange.ExchangeService.ExchangeServiceType);

            return exchangeServices;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create(CreateACDViewModel viewModel)
        {
            var services = await GetServices();

            viewModel.ExchangeServices = new SelectList(services, nameof(ExternalServiceData.Id),
                nameof(ExternalServiceData.Name));
            if (viewModel.CryptoAmount <= 0)
            {
                ModelState.AddModelError(nameof(viewModel.CryptoAmount), "Amount needs to be more than 0.");
            }
            
            if (ModelState.IsValid)
            {
                var serviceData =
                    await _externalServiceManager.GetExternalServiceData(viewModel.SelectedExchangeServiceId, GetUserId());
                var exchangeService = new ExchangeService(serviceData);
                var symbols = (await exchangeService.ConstructClient().GetMarketSymbolsAsync()).ToArray();
                if (!symbols.Contains(viewModel.MarketSymbol))
                {
                    viewModel.AddModelError(nameof(viewModel.MarketSymbol), $"The market symbols you entered is invalid. Please choose from the following: {string.Join(",", symbols)}", ModelState);
                }
            }
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            return await SetItUp(viewModel);
        }
        
        protected string GetUserId()
        {
            return _userManager.GetUserId(User);
        }

        private async Task<IActionResult> SetItUp(CreateACDViewModel vm)
        {
            var presetName = $"Generated_ACD";
            
            var recipe = new Recipe()
            {
                Name = presetName,
                Description = "Generated from a preset",
                UserId = _userManager.GetUserId(User),
                Enabled = false
            };
            await _recipeManager.AddOrUpdateRecipe(recipe);

            var recipeTrigger = new RecipeTrigger()
            {
                TriggerId = new TimerTrigger().Id,
                RecipeId = recipe.Id
            };

            recipeTrigger.Set(new TimerTriggerParameters()
            {
                StartOn = vm.StartOn,
               TriggerEvery = vm.TriggerEvery,
               TriggerEveryAmount = vm.TriggerEveryAmount
               
            });
            await _recipeManager.AddOrUpdateRecipeTrigger(recipeTrigger);

            var recipeActionGroup = new RecipeActionGroup()
            {
                RecipeId = recipe.Id
            };

                await _recipeManager.AddRecipeActionGroup(recipeActionGroup);

                var tradeAction = new RecipeAction()
                {
                    RecipeId = recipe.Id,
                    RecipeActionGroupId = recipeActionGroup.Id,
                    ActionId = new PlaceOrderDataActionHandler().ActionId,
                    ExternalServiceId = vm.SelectedExchangeServiceId,
                    Order = 0,
                    DataJson = JsonConvert.SerializeObject(new PlaceOrderData()
                    {
                        Amount = vm.CryptoAmount.ToString(CultureInfo.InvariantCulture),
                        IsMargin = vm.IsSell,
                        IsBuy = !vm.IsSell,
                        MarketSymbol = vm.MarketSymbol,
                        OrderType = OrderType.Market
                    })
                };

                await _recipeManager.AddOrUpdateRecipeAction(tradeAction);
            
            
            return RedirectToAction("EditRecipe", "Recipes", new
            {
                id = recipe.Id,
                statusMessage =
                    "Preset generated. Recipe is currently disabled for now. Please verify details are correct before enabling!"
            });
        }
    }

    public class CreateACDViewModel
    {
        public SelectList ExchangeServices { get; set; }
        
        [Display(Name = "Existing Exchange Store")]
        [Required]
        public string SelectedExchangeServiceId { get; set; }
        
        [Display(Name = "The trading pair on the exchange")]
        [Required]
        public string MarketSymbol { get; set; }

        [Display(Name = "Is it a sell market order?")]
        [Required]
        public bool IsSell { get; set; } = true;
        
        [Display(Name = "How much do you want to sell?")]
        [Required]
        public decimal CryptoAmount { get; set; }

        [Required]
        [Display(Name = "Trigger every")]
        public int TriggerEveryAmount { get; set; } = 1;

        [Required]
        public TimerTriggerParameters.TimerResetEvery TriggerEvery { get; set; } =
            TimerTriggerParameters.TimerResetEvery.Day;
        [Display(Name = "Start from")]
        public DateTime? StartOn { get; set; }
    }
}