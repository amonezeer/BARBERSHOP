using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADO_EF.Data.Entities
{
    public class UserImage
    {
        public Guid UserId { get; set; }          
        public string ImageUrl { get; set; } = null!; 
        public int? Order { get; set; }

        public User User { get; set; } = null!;
    }
}
