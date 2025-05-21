namespace ADO_EF.Data.Entities
{
    public class Barber
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; 
        public double Rating { get; set; } = 0;         
        public int Reviews { get; set; } = 0;           
        public string ImageSource { get; set; } = string.Empty; 
    }
}