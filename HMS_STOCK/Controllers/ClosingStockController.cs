using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using HMS_STOCK.Models;

namespace HMS_STOCK.Controllers
{
    public class ClosingStockController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: ClosingStock
        public ActionResult Index(int? materialGroupId)
        {
            // Get Material Groups for dropdown
            var materialGroups = db.Database.SqlQuery<MaterialGroupMaster>(
                "SELECT MTRLGID, MTRLGDESC FROM MATERIALGROUPMASTER ORDER BY MTRLGDESC")
                .ToList();

            ViewBag.MaterialGroups = new SelectList(materialGroups, "MTRLGID", "MTRLGDESC", materialGroupId);
            ViewBag.SelectedMaterialGroup = materialGroupId;

            // Get Stock Data with optional filter
            List<StockMaster_2526> stockData;
            if (materialGroupId.HasValue)
            {
                stockData = db.Database.SqlQuery<StockMaster_2526>(
                    "SELECT * FROM StockMaster_2526 WHERE MTRLGID = @p0", materialGroupId.Value)
                    .ToList();
            }
            else
            {
                stockData = db.Database.SqlQuery<StockMaster_2526>("SELECT * FROM StockMaster_2526").ToList();
            }

            return View(stockData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
