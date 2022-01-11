using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjekatNBP.Models
{
    public class Ads
    {
        public List<Category> CategoryList { get; set; }
        public List<Ad> AdList { get; set; }
        public List<Ad> AdRecomendList { get; set; }
    }
}
