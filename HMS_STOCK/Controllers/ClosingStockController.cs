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
        public ActionResult Index()
        {
            var stockData = db.Database.SqlQuery<StockMaster_2526>("SELECT * FROM StockMaster_2526").ToList();
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
