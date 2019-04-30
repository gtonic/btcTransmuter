using BtcTransmuter.Extension.BtcPayServer.Actions.GetInvoice;
using BtcTransmuter.Extension.BtcPayServer.HostedServices;
using BtcTransmuter.Tests.Base;

namespace BtcTransmuter.Extension.BtcPayServer.Tests
{
    public class
        GetInvoiceDataActionHandlerTests : BaseActionTest<GetInvoiceDataActionHandler, GetInvoiceData, BtcPayInvoice>
    {
        protected override GetInvoiceDataActionHandler GetActionHandlerInstance(params object[] setupArgs)
        {
            return new GetInvoiceDataActionHandler();
        }
    }
}