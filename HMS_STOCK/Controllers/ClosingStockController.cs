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
            // Get Material Groups for dropdown (only MTRLTID=2 and DISPSTATUS=0)
            var materialGroups = db.Database.SqlQuery<MaterialGroupMaster>(
                "SELECT MTRLGID, MTRLGDESC FROM MATERIALGROUPMASTER WHERE MTRLTID = 2 AND DISPSTATUS = 0 ORDER BY MTRLGDESC")
                .ToList();

            // Calculate totals based on filter
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

            return View(new List<StockMaster_2526>());
        }

        // POST: Get Stock Data via AJAX for DataTables Server-Side Processing
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

                // Map DataTables column index (as sent by the ClosingStock Index view) to database column name
                // View columns: MTRLGDESC, MTRLDESC, BATCHNO, STKEDATE, MTRLSTKQTY, STKPRATE, CLVALUE, CURRENTBATCH, PHYQTY, Action
                string[] columns = new string[] {
                    "MTRLGDESC", "MTRLDESC", "BATCHNO", "STKEDATE", "MTRLSTKQTY",
                    "STKPRATE", "CLVALUE", "CURRENTBATCH", "PHYQTY"
                };

                string sortColumnName = sortColumn < columns.Length ? columns[sortColumn] : "MTRLDESC";

                // Calculate row numbers for paging
                int startRowNum = start + 1;
                int endRowNum = start + length;

                // Execute stored procedure with parameters
                var sql = @"DECLARE @TotalRowsCount int, @FilteredRowsCount int;
                    EXEC [dbo].[pr_Searchclosingstock]
                        @FilterTerm = {0},
                        @SortIndex = {1},
                        @SortDirection = {2},
                        @StartRowNum = {3},
                        @EndRowNum = {4},
                        @TotalRowsCount = @TotalRowsCount OUTPUT,
                        @FilteredRowsCount = @FilteredRowsCount OUTPUT;
                    SELECT @TotalRowsCount as TotalRowsCount, @FilteredRowsCount as FilteredRowsCount;";

                // Get total counts
                int totalCount = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM StockMaster_2526").FirstOrDefault();

                int filteredCount = totalCount;

                // Get filtered data
                List<StockMaster_2526> stockData;
                if (materialGroupId.HasValue && materialGroupId.Value > 0)
                {
                    var query = "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526 WHERE MTRLGID = @p0) AS T WHERE T.RowNum BETWEEN @p1 AND @p2";
                    stockData = db.Database.SqlQuery<StockMaster_2526>(
                        query,
                        materialGroupId.Value, startRowNum, endRowNum).ToList();
                    
                    filteredCount = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM StockMaster_2526 WHERE MTRLGID = @p0", materialGroupId.Value).FirstOrDefault();
                }
                else if (!string.IsNullOrEmpty(searchValue))
                {
                    var query = @"SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + @") AS RowNum FROM StockMaster_2526 
                        WHERE MTRLGDESC LIKE @p0 OR MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR TRANREFNAME LIKE @p0) AS T 
                        WHERE T.RowNum BETWEEN @p1 AND @p2";
                    stockData = db.Database.SqlQuery<StockMaster_2526>(
                        query,
                        "%" + searchValue + "%", startRowNum, endRowNum).ToList();
                    
                    filteredCount = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(*) FROM StockMaster_2526 
                        WHERE MTRLGDESC LIKE @p0 OR MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR TRANREFNAME LIKE @p0",
                        "%" + searchValue + "%").FirstOrDefault();
                }
                else
                {
                    stockData = db.Database.SqlQuery<StockMaster_2526>(
                        "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526) AS T WHERE T.RowNum BETWEEN @p0 AND @p1",
                        startRowNum, endRowNum).ToList();
                }

                // Convert data to anonymous objects with formatted dates to avoid /Date()/ serialization
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
                    item.PHYQTY
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

        [HttpGet]
        public JsonResult GetRow(int stkBid)
        {
            try
            {
                var row = db.Database.SqlQuery<StockMaster_2526>(
                        "SELECT TOP 1 * FROM StockMaster_2526 WHERE STKBID = @p0",
                        stkBid)
                    .FirstOrDefault();

                if (row == null)
                {
                    return Json(new { ok = false, message = "Record not found" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    ok = true,
                    data = new
                    {
                        row.STKBID,
                        row.TRANREFID,
                        row.MTRLGDESC,
                        row.MTRLDESC,
                        row.BATCHNO,
                        STKEDATE = row.STKEDATE.ToString("yyyy-MM-dd"),
                        row.MTRLSTKQTY,
                        row.CURRENTBATCH,
                        row.PHYQTY
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
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
