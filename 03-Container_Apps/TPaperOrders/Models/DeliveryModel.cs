using System.Text.Json;

namespace TPaperOrders
{
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v11.0.0.0)")]
    public partial class DeliveryModel
    {
        public int Id { get; set; }

        public double Number { get; set; }

        public int ClientId { get; set; }

        public int EdiOrderId { get; set; }

        public int ProductCode { get; set; }

        public int ProductId { get; set; }

        public string Notes { get; set; }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties = new System.Collections.Generic.Dictionary<string, object>();

        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties; }
            set { _additionalProperties = value; }
        }
    }
}