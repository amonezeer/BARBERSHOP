using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADO_EF.Data.Entities
{
    public class UserRole
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;

        public List<UserAccess> UserAccesses { get; set; } = [];
    }
}
