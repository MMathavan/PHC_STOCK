using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using System.Web.Mvc;
using HMS_STOCK.Models;

namespace HMS_STOCK.Controllers
{
    public class DirectManualEntryController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        private class MaterialGroupDropdownItem
        {
            public int MTRLGID { get; set; }
            public string MTRLGDESC { get; set; }
        }

        private class MaterialDropdownItem
        {
            public int MTRLID { get; set; }
            public string MTRLDESC { get; set; }
        }

        private void LoadMaterialGroups(int? selectedGroupId = null)
        {
            var groups = db.Database.SqlQuery<MaterialGroupDropdownItem>(
                "SELECT MTRLGID, MTRLGDESC FROM MATERIALGROUPMASTER WHERE MTRLTID = 2 AND DISPSTATUS = 0 ORDER BY MTRLGDESC").ToList();

            ViewBag.MaterialGroups = new SelectList(groups, "MTRLGID", "MTRLGDESC", selectedGroupId);
        }

        private void LoadMaterialsByGroup(int? materialGroupId, int? selectedMtrlId = null)
        {
            if (!materialGroupId.HasValue || materialGroupId.Value <= 0)
            {
                ViewBag.Materials = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text", selectedMtrlId);
                return;
            }

            var materials = db.Database.SqlQuery<MaterialDropdownItem>(
                "SELECT MTRLID, MTRLDESC FROM MATERIALMASTER WHERE MTRLGID = @p0 AND (DISPSTATUS = 0 OR DISPSTATUS IS NULL) ORDER BY MTRLDESC",
                materialGroupId.Value).ToList();

            ViewBag.Materials = new SelectList(materials, "MTRLID", "MTRLDESC", selectedMtrlId);
        }

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.DirectManualEntrySearch = Session != null ? (Session["DirectManualEntrySearch"] ?? string.Empty) : string.Empty;
            return View(new List<StockMaster_2526>());
        }

        [HttpPost]
        public JsonResult GetManualEntryData(DataTableRequest request)
        {
            try
            {
                int start = request.Start;
                int length = request.Length;
                string searchValue = request.Search?.Value ?? "";
                int sortColumn = request.Order != null && request.Order.Count > 0 ? request.Order[0].Column : 0;
                string sortDirection = request.Order != null && request.Order.Count > 0 ? request.Order[0].Dir : "asc";

                if (Session != null)
                {
                    Session["DirectManualEntrySearch"] = searchValue ?? string.Empty;
                }

                // View columns: MTRLGDESC, MTRLDESC, BATCHNO, STKEDATE, MTRLSTKQTY, STKPRATE, CLVALUE, CURRENTBATCH, PHYQTY
                string[] columns = new string[] {
                    "MTRLGDESC", "MTRLDESC", "BATCHNO", "STKEDATE", "MTRLSTKQTY",
                    "STKPRATE", "CLVALUE", "CURRENTBATCH", "PHYQTY"
                };

                string sortColumnName = sortColumn < columns.Length ? columns[sortColumn] : "MTRLDESC";

                int startRowNum = start + 1;
                int endRowNum = start + length;

                int totalCount = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM StockMaster_2526 WHERE STKBID = 0").FirstOrDefault();

                int filteredCount = totalCount;
                List<StockMaster_2526> rows;

                bool hasSearch = !string.IsNullOrWhiteSpace(searchValue);

                if (hasSearch)
                {
                    var query = "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526 WHERE STKBID = 0 AND (MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p0)) AS T WHERE T.RowNum BETWEEN @p1 AND @p2";

                    rows = db.Database.SqlQuery<StockMaster_2526>(
                        query,
                        "%" + searchValue + "%", startRowNum, endRowNum).ToList();

                    filteredCount = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(*) FROM StockMaster_2526
                          WHERE STKBID = 0 AND (MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p0)",
                        "%" + searchValue + "%").FirstOrDefault();
                }
                else
                {
                    rows = db.Database.SqlQuery<StockMaster_2526>(
                        "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526 WHERE STKBID = 0) AS T WHERE T.RowNum BETWEEN @p0 AND @p1",
                        startRowNum, endRowNum).ToList();
                }

                var formattedData = rows.Select(item => new
                {
                    item.STKBID,
                    item.MTRLGDESC,
                    item.MTRLDESC,
                    item.BATCHNO,
                    STKEDATE = item.STKEDATE.ToString("yyyy-MM-dd"),
                    item.MTRLSTKQTY,
                    item.STKPRATE,
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
        public ActionResult Add()
        {
            LoadMaterialGroups();
            LoadMaterialsByGroup(null);
            return View();
        }

        [HttpGet]
        public JsonResult GetMaterialsByGroup(int materialGroupId)
        {
            try
            {
                var materials = db.Database.SqlQuery<MaterialDropdownItem>(
                    "SELECT MTRLID, MTRLDESC FROM MATERIALMASTER WHERE MTRLGID = @p0 AND (DISPSTATUS = 0 OR DISPSTATUS IS NULL) ORDER BY MTRLDESC",
                    materialGroupId).ToList();

                var data = materials.Select(m => new { id = m.MTRLID, text = m.MTRLDESC }).ToList();
                return Json(data, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(int? materialGroupId, int? mtrlid)
        {
            if (!materialGroupId.HasValue || materialGroupId.Value <= 0)
            {
                LoadMaterialGroups(materialGroupId);
                LoadMaterialsByGroup(materialGroupId, mtrlid);
                ViewBag.ErrorMessage = "Please select a Material Group.";
                return View();
            }

            if (!mtrlid.HasValue || mtrlid.Value <= 0)
            {
                LoadMaterialGroups(materialGroupId);
                LoadMaterialsByGroup(materialGroupId, mtrlid);
                ViewBag.ErrorMessage = "Please select a Material.";
                return View();
            }

            try
            {
                var p = new SqlParameter("@mtrlid", mtrlid.Value);
                db.Database.ExecuteSqlCommand("EXEC PR_MANUALSTOCKENTRY_INSERT @mtrlid", p);

                TempData["SuccessMessage"] = "Saved Successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                LoadMaterialGroups(materialGroupId);
                LoadMaterialsByGroup(materialGroupId, mtrlid);
                ViewBag.ErrorMessage = ex.Message;
                return View();
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
