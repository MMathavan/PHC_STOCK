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

                // Map DataTables column index to database column name
                string[] columns = new string[] {
                    "STKBID", "TRANREFID", "TRANREFNAME", "TRANDREFGID", "MTRLGID",
                    "TRANDREFID", "MTRLGDESC", "MTRLDESC", "DACHEADID", "PACKMID",
                    "BATCHNO", "STKEDATE", "MTRLSTKQTY", "STKPRATE", "STKMRP",
                    "ASTKSRATE", "HSNID", "TRANBCGSTEXPRN", "TRANBSGSTEXPRN", "TRANBIGSTEXPRN",
                    "TRANBCGSTAMT", "TRANBSGSTAMT", "TRANBIGSTAMT", "CLVALUE"
                };

                string sortColumnName = sortColumn < columns.Length ? columns[sortColumn] : "TRANREFID";

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
                    stockData = db.Database.SqlQuery<StockMaster_2526>(
                        "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY TRANREFID) AS RowNum FROM StockMaster_2526 WHERE MTRLGID = @p0) AS T WHERE T.RowNum BETWEEN @p1 AND @p2",
                        materialGroupId.Value, startRowNum, endRowNum).ToList();
                    
                    filteredCount = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM StockMaster_2526 WHERE MTRLGID = @p0", materialGroupId.Value).FirstOrDefault();
                }
                else if (!string.IsNullOrEmpty(searchValue))
                {
                    stockData = db.Database.SqlQuery<StockMaster_2526>(
                        @"SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY TRANREFID) AS RowNum FROM StockMaster_2526 
                        WHERE MTRLGDESC LIKE @p0 OR MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR TRANREFNAME LIKE @p0) AS T 
                        WHERE T.RowNum BETWEEN @p1 AND @p2",
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
                    item.CLVALUE
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
