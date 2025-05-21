namespace ADO_EF.Data.Entities
{
    public class UserAccess
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string Dk { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
    }
}