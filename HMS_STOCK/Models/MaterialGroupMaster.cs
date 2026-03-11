using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS_STOCK.Models
{
    [Table("MATERIALGROUPMASTER")]
    public class MaterialGroupMaster
    {
        [Key]
        public int MTRLGID { get; set; }

        public int? MTRLTID { get; set; }

        [StringLength(100)]
        public string MTRLGDESC { get; set; }

        [StringLength(15)]
        public string MTRLGCODE { get; set; }

        [StringLength(100)]
        public string CUSRID { get; set; }

        public int? LMUSRID { get; set; }

        public short? DISPSTATUS { get; set; }

        public DateTime? PRCSDATE { get; set; }

        public int? MACHEADID { get; set; }
    }
}
