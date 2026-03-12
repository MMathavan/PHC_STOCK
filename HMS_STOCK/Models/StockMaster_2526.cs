using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HMS_STOCK.Models
{
    [Table("StockMaster_2526")]
    public class StockMaster_2526
    {
        public int? STKBID { get; set; }

        [Key]
        public int TRANREFID { get; set; }

        [Required]
        [StringLength(100)]
        public string TRANREFNAME { get; set; }

        [Required]
        public int TRANDREFGID { get; set; }

        public int? MTRLGID { get; set; }

        public int? TRANDREFID { get; set; }

        [StringLength(100)]
        public string MTRLGDESC { get; set; }

        [StringLength(100)]
        public string MTRLDESC { get; set; }

        [Required]
        public int DACHEADID { get; set; }

        [Required]
        public int PACKMID { get; set; }

        [Required]
        [StringLength(50)]
        public string BATCHNO { get; set; }

        [Required]
        public DateTime STKEDATE { get; set; }

        public decimal? MTRLSTKQTY { get; set; }

        [Required]
        public decimal STKPRATE { get; set; }

        [Required]
        public decimal STKMRP { get; set; }

        [Required]
        public decimal ASTKSRATE { get; set; }

        [Required]
        public int HSNID { get; set; }

        [Required]
        public decimal TRANBCGSTEXPRN { get; set; }

        [Required]
        public decimal TRANBSGSTEXPRN { get; set; }

        [Required]
        public decimal TRANBIGSTEXPRN { get; set; }

        [Required]
        public decimal TRANBCGSTAMT { get; set; }

        [Required]
        public decimal TRANBSGSTAMT { get; set; }

        [Required]
        public decimal TRANBIGSTAMT { get; set; }

        public decimal? CLVALUE { get; set; }

        [StringLength(50)]
        public string CURRENTBATCH { get; set; }

        public decimal? PHYQTY { get; set; }

        [StringLength(100)]
        public string CUSRID { get; set; }

        [StringLength(100)]
        public string LMUSRID { get; set; }

        public DateTime? PRCSDATE { get; set; }
    }
}
