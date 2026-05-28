using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.Payments.Saman.Models
{
    public class SamanReturnModel
    {
        [FromForm(Name = "Status")]
        public int Status { get; set; }

        [FromForm(Name = "RefNum")]
        public string RefNum { get; set; }

        [FromForm(Name = "ResNum")]
        public string ResNum { get; set; }

        [FromForm(Name = "TraceNo")]
        public string TraceNo { get; set; }

        [FromForm(Name = "SecurePan")]
        public string SecurePan { get; set; }

        [FromForm(Name = "TerminalId")]
        public string TerminalId { get; set; }

        [FromForm(Name = "Token")]
        public string Token { get; set; }
    }
}
