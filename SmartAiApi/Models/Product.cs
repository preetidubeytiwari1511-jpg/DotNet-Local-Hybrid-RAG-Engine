namespace SmartAiApi.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Stock { get; set; }
        public decimal Price { get; set; }
    }
    public class Order
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; } // Foreign Key Relation
        public int QuantityOrdered { get; set; }
        public string CustomerName { get; set; } = string.Empty;
    }
}
