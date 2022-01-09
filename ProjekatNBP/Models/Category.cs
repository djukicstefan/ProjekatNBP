using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjekatNBP.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Ad> Ads { get; set; }

        public Category()
        {
            Ads = new List<Ad>();
        }
    }
}
