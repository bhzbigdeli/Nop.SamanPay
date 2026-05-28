namespace Nop.Plugin.Payments.Saman
{
    public static class SamanPaymentDefaults
    {
        public const string SystemName = "Payments.Saman";

        public const string PaymentTokenAttribute = "SamanBankPaymentToken";

        public const string TokenRequestUrl = "https://sep.shaparak.ir/onlinepg/onlinepg";
        public const string PaymentPageUrl = "https://sep.shaparak.ir/OnlinePG/OnlinePG";
        public const string VerifyUrl = "https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/VerifyTransaction";
    }
}
