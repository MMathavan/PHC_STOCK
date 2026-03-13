using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.Mvc;
using HMS_STOCK.Models;

namespace HMS_STOCK.Controllers
{
    public class ClosingStockEntryController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        [HttpGet]
        public ActionResult Index(int? materialGroupId)
        {
            var materialGroups = db.Database.SqlQuery<MaterialGroupMaster>(
                    "SELECT MTRLGID, MTRLGDESC FROM MATERIALGROUPMASTER WHERE MTRLTID = 2 AND DISPSTATUS = 0 ORDER BY MTRLGDESC")
                .ToList();

            decimal totalQty = 0;
            decimal totalValue = 0;

            if (materialGroupId.HasValue && materialGroupId.Value > 0)
            {
                totalQty = db.Database.SqlQuery<decimal>(
                    "SELECT ISNULL(SUM(MTRLSTKQTY), 0) FROM StockMaster_2526 WHERE MTRLGID = @p0",
                    materialGroupId.Value).FirstOrDefault();

                totalValue = db.Database.SqlQuery<decimal>(
                    "SELECT ISNULL(SUM(CLVALUE), 0) FROM StockMaster_2526 WHERE MTRLGID = @p0",
                    materialGroupId.Value).FirstOrDefault();
            }
            else
            {
                totalQty = db.Database.SqlQuery<decimal>(
                    "SELECT ISNULL(SUM(MTRLSTKQTY), 0) FROM StockMaster_2526").FirstOrDefault();

                totalValue = db.Database.SqlQuery<decimal>(
                    "SELECT ISNULL(SUM(CLVALUE), 0) FROM StockMaster_2526").FirstOrDefault();
            }

            ViewBag.MaterialGroups = new SelectList(materialGroups, "MTRLGID", "MTRLGDESC", materialGroupId);
            ViewBag.SelectedMaterialGroup = materialGroupId;
            ViewBag.TotalQuantity = totalQty;
            ViewBag.TotalValue = totalValue;
            ViewBag.ClosingStockEntrySearch = Session != null ? (Session["ClosingStockEntrySearch"] ?? string.Empty) : string.Empty;

            return View(new List<StockMaster_2526>());
        }

        [HttpPost]
        public JsonResult GetStockData(DataTableRequest request)
        {
            try
            {
                int? materialGroupId = request.MaterialGroupId;
                int start = request.Start;
                int length = request.Length;
                string searchValue = request.Search?.Value ?? "";
                int sortColumn = request.Order != null && request.Order.Count > 0 ? request.Order[0].Column : 0;
                string sortDirection = request.Order != null && request.Order.Count > 0 ? request.Order[0].Dir : "asc";

                if (Session != null)
                {
                    Session["ClosingStockEntrySearch"] = searchValue ?? string.Empty;
                }

                // View columns (excluding Action): keep in sync with view's DataTables columns order
                string[] columns = new string[]
                {
                    "STKBID",
                    "TRANREFID",
                    "TRANREFNAME",
                    "TRANDREFGID",
                    "MTRLGID",
                    "TRANDREFID",
                    "MTRLGDESC",
                    "MTRLDESC",
                    "DACHEADID",
                    "PACKMID",
                    "BATCHNO",
                    "STKEDATE",
                    "MTRLSTKQTY",
                    "STKPRATE",
                    "STKMRP",
                    "ASTKSRATE",
                    "HSNID",
                    "TRANBCGSTEXPRN",
                    "TRANBSGSTEXPRN",
                    "TRANBIGSTEXPRN",
                    "TRANBCGSTAMT",
                    "TRANBSGSTAMT",
                    "TRANBIGSTAMT",
                    "CLVALUE",
                    "CURRENTBATCH",
                    "PHYQTY",
                    "CUSRID",
                    "LMUSRID",
                    "PRCSDATE"
                };

                string sortColumnName = sortColumn < columns.Length ? columns[sortColumn] : "MTRLDESC";

                int startRowNum = start + 1;
                int endRowNum = start + length;

                int totalCount = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM StockMaster_2526").FirstOrDefault();

                int filteredCount = totalCount;
                List<StockMaster_2526> stockData;

                bool hasMaterialFilter = materialGroupId.HasValue && materialGroupId.Value > 0;
                bool hasSearch = !string.IsNullOrWhiteSpace(searchValue);

                if (hasMaterialFilter)
                {
                    string where = "WHERE MTRLGID = @p0";
                    if (hasSearch)
                    {
                        where += " AND (MTRLDESC LIKE @p1 OR BATCHNO LIKE @p1 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p1)";
                    }

                    var query = "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526 " + where + ") AS T WHERE T.RowNum BETWEEN @p" + (hasSearch ? "2" : "1") + " AND @p" + (hasSearch ? "3" : "2") + ";";

                    if (hasSearch)
                    {
                        stockData = db.Database.SqlQuery<StockMaster_2526>(
                            query,
                            materialGroupId.Value, "%" + searchValue + "%", startRowNum, endRowNum).ToList();

                        filteredCount = db.Database.SqlQuery<int>(
                            "SELECT COUNT(*) FROM StockMaster_2526 " + where,
                            materialGroupId.Value, "%" + searchValue + "%").FirstOrDefault();
                    }
                    else
                    {
                        stockData = db.Database.SqlQuery<StockMaster_2526>(
                            query,
                            materialGroupId.Value, startRowNum, endRowNum).ToList();

                        filteredCount = db.Database.SqlQuery<int>(
                            "SELECT COUNT(*) FROM StockMaster_2526 " + where,
                            materialGroupId.Value).FirstOrDefault();
                    }
                }
                else if (hasSearch)
                {
                    var query = "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526 WHERE MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p0) AS T WHERE T.RowNum BETWEEN @p1 AND @p2";

                    stockData = db.Database.SqlQuery<StockMaster_2526>(
                        query,
                        "%" + searchValue + "%", startRowNum, endRowNum).ToList();

                    filteredCount = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(*) FROM StockMaster_2526
                          WHERE MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p0",
                        "%" + searchValue + "%").FirstOrDefault();
                }
                else
                {
                    stockData = db.Database.SqlQuery<StockMaster_2526>(
                        "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526) AS T WHERE T.RowNum BETWEEN @p0 AND @p1",
                        startRowNum, endRowNum).ToList();
                }

                var formattedData = stockData.Select(item => new
                {
                    item.STKBID,
                    item.TRANREFID,
                    item.TRANREFNAME,
                    item.TRANDREFGID,
                    item.MTRLGID,
                    item.TRANDREFID,
                    item.MTRLGDESC,
                    item.MTRLDESC,
                    item.DACHEADID,
                    item.PACKMID,
                    item.BATCHNO,
                    STKEDATE = item.STKEDATE.ToString("yyyy-MM-dd"),
                    item.MTRLSTKQTY,
                    item.STKPRATE,
                    item.STKMRP,
                    item.ASTKSRATE,
                    item.HSNID,
                    item.TRANBCGSTEXPRN,
                    item.TRANBSGSTEXPRN,
                    item.TRANBIGSTEXPRN,
                    item.TRANBCGSTAMT,
                    item.TRANBSGSTAMT,
                    item.TRANBIGSTAMT,
                    item.CLVALUE,
                    item.CURRENTBATCH,
                    item.PHYQTY,
                    item.CUSRID,
                    item.LMUSRID,
                    PRCSDATE = item.PRCSDATE.HasValue ? item.PRCSDATE.Value.ToString("yyyy-MM-dd") : ""
                }).ToList();

                return Json(new
                {
                    draw = request.Draw,
                    recordsTotal = totalCount,
                    recordsFiltered = filteredCount,
                    data = formattedData,
                    error = (string)null
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = request.Draw,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<StockMaster_2526>(),
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult UpdatePhysical(int stkBid, string currentBatch, decimal? phyQty)
        {
            try
            {
                string currentUser = null;
                if (Session != null && Session["CUSRID"] != null)
                {
                    currentUser = Session["CUSRID"].ToString();
                }
                if (string.IsNullOrWhiteSpace(currentUser) && User != null && User.Identity != null)
                {
                    currentUser = User.Identity.Name;
                }
                if (currentUser == null) currentUser = string.Empty;

                db.Database.ExecuteSqlCommand(
                    @"UPDATE StockMaster_2526
                      SET CURRENTBATCH = @p1,
                          PHYQTY = @p2,
                          CUSRID = CASE WHEN CUSRID IS NULL OR LTRIM(RTRIM(CUSRID)) = '' THEN @p3 ELSE CUSRID END,
                          LMUSRID = @p3,
                          PRCSDATE = GETDATE()
                      WHERE STKBID = @p0",
                    stkBid,
                    (object)currentBatch ?? DBNull.Value,
                    (object)phyQty ?? DBNull.Value,
                    currentUser);

                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeletePhysical(int stkBid)
        {
            try
            {
                string currentUser = null;
                if (Session != null && Session["CUSRID"] != null)
                {
                    currentUser = Session["CUSRID"].ToString();
                }
                if (string.IsNullOrWhiteSpace(currentUser) && User != null && User.Identity != null)
                {
                    currentUser = User.Identity.Name;
                }
                if (currentUser == null) currentUser = string.Empty;

                db.Database.ExecuteSqlCommand(
                    @"UPDATE StockMaster_2526
                      SET CURRENTBATCH = NULL,
                          PHYQTY = NULL,
                          CUSRID = CASE WHEN CUSRID IS NULL OR LTRIM(RTRIM(CUSRID)) = '' THEN @p1 ELSE CUSRID END,
                          LMUSRID = @p1,
                          PRCSDATE = GETDATE()
                      WHERE STKBID = @p0",
                    stkBid,
                    currentUser);

                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message });
            }
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
