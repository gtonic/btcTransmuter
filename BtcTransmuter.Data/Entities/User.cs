using System.Collections.Generic;
using BtcTransmuter.Data.Encryption;
using BtcTransmuter.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace BtcTransmuter.Data.Entities
{
    public class User : IdentityUser, IHasJsonData
    {
        public List<Recipe> Recipes { get; set; }

        [Encrypted] public string DataJson { get; set; }
    }
}