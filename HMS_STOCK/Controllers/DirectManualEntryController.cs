using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using HMS_STOCK.Models;

namespace HMS_STOCK.Controllers
{
    public class DirectManualEntryController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        private static T GetDictValue<T>(Dictionary<string, object> dict, string key, T defaultValue = default(T))
        {
            if (dict == null || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            object raw;
            if (!dict.TryGetValue(key, out raw) || raw == null || raw == DBNull.Value)
            {
                return defaultValue;
            }

            try
            {
                if (raw is T)
                {
                    return (T)raw;
                }
                return (T)Convert.ChangeType(raw, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        private static object ToDbValue(object val)
        {
            return val == null ? (object)DBNull.Value : val;
        }

        private string GetCurrentUserId()
        {
            if (Session != null && Session["CUSRID"] != null)
            {
                var s = Convert.ToString(Session["CUSRID"]);
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }

            return User != null && User.Identity != null ? (User.Identity.Name ?? string.Empty) : string.Empty;
        }

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

        private class MaterialGroupByMaterialItem
        {
            public int MTRLGID { get; set; }
            public string MTRLGDESC { get; set; }
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

        private void LoadAllMaterials(int? selectedMtrlId = null)
        {
            var materials = db.Database.SqlQuery<MaterialDropdownItem>(
                "SELECT DISTINCT MTRLID, MTRLDESC FROM VW_MATEIRAL_DETAIL_STOCK_2526 ORDER BY MTRLDESC").ToList();

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
                    item.SID,
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
        public ActionResult Edit(int id)
        {
            var row = db.Database.SqlQuery<StockMaster_2526>(
                "SELECT TOP 1 * FROM StockMaster_2526 WHERE SID = @p0", id).FirstOrDefault();

            if (row == null)
            {
                return HttpNotFound();
            }

            int selectedMtrlId = (row.TRANDREFID.HasValue && row.TRANDREFID.Value > 0)
                ? row.TRANDREFID.Value
                : row.TRANREFID;

            LoadAllMaterials(selectedMtrlId);
            ViewBag.IsEdit = true;
            ViewBag.SID = row.SID;
            ViewBag.CurrentBatch = row.CURRENTBATCH;
            ViewBag.PhyQty = row.PHYQTY;
            ViewBag.ExpiryDate = row.STKEDATE.ToString("yyyy-MM-dd");
            return View("Add");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int SID, int? mtrlid, string currentBatch, decimal? phyQty, DateTime? expiryDate)
        {
            if (!mtrlid.HasValue || mtrlid.Value <= 0)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.IsEdit = true;
                ViewBag.SID = SID;
                ViewBag.ErrorMessage = "Please select a Material.";
                return View("Add");
            }

            if (!expiryDate.HasValue)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.IsEdit = true;
                ViewBag.SID = SID;
                ViewBag.CurrentBatch = currentBatch;
                ViewBag.PhyQty = phyQty;
                ViewBag.ExpiryDate = string.Empty;
                ViewBag.ErrorMessage = "Please select Expiry Date.";
                return View("Add");
            }

            try
            {
                string userId = GetCurrentUserId();
                DateTime prcsDate = DateTime.Now;
                DateTime newExpiry = expiryDate.Value;

                db.Database.ExecuteSqlCommand(@"
UPDATE StockMaster_2526
SET
    TRANDREFID = @p0,
    STKEDATE = @p1,
    LMUSRID = @p2,
    PRCSDATE = @p3
WHERE SID = @p4",
                    mtrlid.Value,
                    newExpiry,
                    ToDbValue(userId),
                    prcsDate,
                    SID);

                TempData["SuccessMessage"] = "Updated Successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.IsEdit = true;
                ViewBag.SID = SID;
                ViewBag.CurrentBatch = currentBatch;
                ViewBag.PhyQty = phyQty;
                ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                ViewBag.ErrorMessage = ex.Message;
                return View("Add");
            }
        }

        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                int affected = db.Database.ExecuteSqlCommand(
                    "DELETE FROM StockMaster_2526 WHERE SID = @p0", id);

                if (affected <= 0)
                {
                    return Json(new { ok = false, message = "Record not found." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { ok = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult Add()
        {
            LoadAllMaterials();
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

        [HttpGet]
        public JsonResult GetMaterialDetails(int mtrlid)
        {
            try
            {
                var opening = GetTop1RowAsDictionary("Z_OPENING_SUPPLIER_HSN_DETAIL_ASSGN_001", mtrlid);
                var material = GetTop1RowAsDictionary("Z_VW_MATERIAL_HSN_DETAIL_ASSGN", mtrlid);

                var group = db.Database.SqlQuery<MaterialGroupByMaterialItem>(
                    @"SELECT TOP 1 g.MTRLGID, g.MTRLGDESC
                      FROM MATERIALMASTER m
                      INNER JOIN MATERIALGROUPMASTER g ON m.MTRLGID = g.MTRLGID
                      WHERE m.MTRLID = @p0",
                    mtrlid).FirstOrDefault();

                return Json(new
                {
                    ok = true,
                    opening,
                    material,
                    group
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { ok = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private Dictionary<string, object> GetTop1RowAsDictionary(string viewName, int mtrlid)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string sql = "SELECT TOP 1 * FROM " + viewName + " WHERE MTRLID = @mtrlid";

            var dt = new DataTable();
            using (var conn = new SqlConnection(db.Database.Connection.ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@mtrlid", mtrlid);
                conn.Open();
                da.Fill(dt);
            }

            if (dt.Rows.Count == 0)
            {
                return result;
            }

            var row = dt.Rows[0];
            foreach (DataColumn col in dt.Columns)
            {
                var val = row[col];
                result[col.ColumnName] = val == DBNull.Value ? null : val;
            }

            return result;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(int? mtrlid, string currentBatch, decimal? phyQty, DateTime? expiryDate)
        {
            if (!mtrlid.HasValue || mtrlid.Value <= 0)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                ViewBag.ErrorMessage = "Please select a Material.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(currentBatch))
            {
                LoadAllMaterials(mtrlid);
                ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                ViewBag.ErrorMessage = "Please enter Current Batch.";
                return View();
            }

            if (!phyQty.HasValue)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                ViewBag.ErrorMessage = "Please enter Physical Quantity.";
                return View();
            }

            if (!expiryDate.HasValue)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.ExpiryDate = string.Empty;
                ViewBag.ErrorMessage = "Please select Expiry Date.";
                return View();
            }

            try
            {
                var opening = GetTop1RowAsDictionary("Z_OPENING_SUPPLIER_HSN_DETAIL_ASSGN_001", mtrlid.Value);
                var material = GetTop1RowAsDictionary("Z_VW_MATERIAL_HSN_DETAIL_ASSGN", mtrlid.Value);

                var mm = db.Database.SqlQuery<MaterialDropdownItem>(
                    "SELECT TOP 1 MTRLID, MTRLDESC FROM MATERIALMASTER WHERE MTRLID = @p0", mtrlid.Value).FirstOrDefault();

                var group = db.Database.SqlQuery<MaterialGroupByMaterialItem>(
                    @"SELECT TOP 1 g.MTRLGID, g.MTRLGDESC
                      FROM MATERIALMASTER m
                      INNER JOIN MATERIALGROUPMASTER g ON m.MTRLGID = g.MTRLGID
                      WHERE m.MTRLID = @p0",
                    mtrlid.Value).FirstOrDefault();

                int stkBid = 0;
                int tranRefId = GetDictValue<int>(opening, "TRANREFID", 0);
                string tranRefName = GetDictValue<string>(opening, "TRANREFNAME", null);

                if (tranRefId <= 0 || string.IsNullOrWhiteSpace(tranRefName))
                {
                    LoadAllMaterials(mtrlid);
                    ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                    ViewBag.ErrorMessage = "Supplier not found for selected Material (TRANREFID/TRANREFNAME missing).";
                    return View();
                }

                int? mtrlGid = group != null ? (int?)group.MTRLGID : GetDictValue<int?>(opening, "MTRLGID", null);
                int trandRefGid = mtrlGid.HasValue ? mtrlGid.Value : 0;
                int? trandRefId = mtrlid.Value;
                string mtrlGDesc = group != null ? group.MTRLGDESC : GetDictValue<string>(opening, "MTRLGDESC", null);
                string mtrlDesc = (mm != null ? mm.MTRLDESC : null) ?? GetDictValue<string>(opening, "TRANDREFNAME", null) ?? GetDictValue<string>(opening, "MTRLDESC", null);
                int dacheadId = 44;
                int packMid = 2;
                string batchNo = currentBatch;
                DateTime stkeDate = expiryDate.Value;
                decimal? mtrlStkQty = phyQty;
                decimal stkPrate = GetDictValue<decimal>(opening, "STKPRATE", 0m);
                decimal stkMrp = GetDictValue<decimal>(opening, "STKMRP", 0m);
                decimal astkSRate = GetDictValue<decimal>(opening, "ASTKSRATE", GetDictValue<decimal>(opening, "ASTKPRATE", 0m));
                int hsnId = GetDictValue<int>(material, "HSNID", GetDictValue<int>(opening, "HSNID", 0));
                decimal cgstExprn = GetDictValue<decimal>(material, "CGSTEXPRN", GetDictValue<decimal>(opening, "TRANBCGSTEXPRN", 0m));
                decimal sgstExprn = GetDictValue<decimal>(material, "SGSTEXPRN", GetDictValue<decimal>(opening, "TRANBSGSTEXPRN", 0m));
                decimal igstExprn = 0m;
                decimal cgstAmt = 0m;
                decimal sgstAmt = 0m;
                decimal igstAmt = 0m;
                decimal? clValue = GetDictValue<decimal?>(opening, "CLVALUE", null);

                string userId = GetCurrentUserId();
                DateTime prcsDate = DateTime.Now;

                var sql =
                    "EXEC PR_MANUALSTOCKENTRY_INSERT " +
                    "@STKBID=@STKBID, " +
                    "@TRANREFID=@TRANREFID, " +
                    "@TRANREFNAME=@TRANREFNAME, " +
                    "@TRANDREFGID=@TRANDREFGID, " +
                    "@MTRLGID=@MTRLGID, " +
                    "@TRANDREFID=@TRANDREFID, " +
                    "@MTRLGDESC=@MTRLGDESC, " +
                    "@MTRLDESC=@MTRLDESC, " +
                    "@DACHEADID=@DACHEADID, " +
                    "@PACKMID=@PACKMID, " +
                    "@BATCHNO=@BATCHNO, " +
                    "@STKEDATE=@STKEDATE, " +
                    "@MTRLSTKQTY=@MTRLSTKQTY, " +
                    "@STKPRATE=@STKPRATE, " +
                    "@STKMRP=@STKMRP, " +
                    "@ASTKSRATE=@ASTKSRATE, " +
                    "@HSNID=@HSNID, " +
                    "@TRANBCGSTEXPRN=@TRANBCGSTEXPRN, " +
                    "@TRANBSGSTEXPRN=@TRANBSGSTEXPRN, " +
                    "@TRANBIGSTEXPRN=@TRANBIGSTEXPRN, " +
                    "@TRANBCGSTAMT=@TRANBCGSTAMT, " +
                    "@TRANBSGSTAMT=@TRANBSGSTAMT, " +
                    "@TRANBIGSTAMT=@TRANBIGSTAMT, " +
                    "@CLVALUE=@CLVALUE, " +
                    "@CURRENTBATCH=@CURRENTBATCH, " +
                    "@PHYQTY=@PHYQTY, " +
                    "@CUSRID=@CUSRID, " +
                    "@LMUSRID=@LMUSRID, " +
                    "@PRCSDATE=@PRCSDATE";

                db.Database.ExecuteSqlCommand(
                    sql,
                    new SqlParameter("@STKBID", stkBid),
                    new SqlParameter("@TRANREFID", tranRefId),
                    new SqlParameter("@TRANREFNAME", tranRefName),
                    new SqlParameter("@TRANDREFGID", trandRefGid),
                    new SqlParameter("@MTRLGID", ToDbValue(mtrlGid)),
                    new SqlParameter("@TRANDREFID", ToDbValue(trandRefId)),
                    new SqlParameter("@MTRLGDESC", ToDbValue(mtrlGDesc)),
                    new SqlParameter("@MTRLDESC", ToDbValue(mtrlDesc)),
                    new SqlParameter("@DACHEADID", dacheadId),
                    new SqlParameter("@PACKMID", packMid),
                    new SqlParameter("@BATCHNO", batchNo ?? string.Empty),
                    new SqlParameter("@STKEDATE", stkeDate),
                    new SqlParameter("@MTRLSTKQTY", ToDbValue(mtrlStkQty)),
                    new SqlParameter("@STKPRATE", stkPrate),
                    new SqlParameter("@STKMRP", stkMrp),
                    new SqlParameter("@ASTKSRATE", astkSRate),
                    new SqlParameter("@HSNID", hsnId),
                    new SqlParameter("@TRANBCGSTEXPRN", cgstExprn),
                    new SqlParameter("@TRANBSGSTEXPRN", sgstExprn),
                    new SqlParameter("@TRANBIGSTEXPRN", igstExprn),
                    new SqlParameter("@TRANBCGSTAMT", cgstAmt),
                    new SqlParameter("@TRANBSGSTAMT", sgstAmt),
                    new SqlParameter("@TRANBIGSTAMT", igstAmt),
                    new SqlParameter("@CLVALUE", ToDbValue(clValue)),
                    new SqlParameter("@CURRENTBATCH", ToDbValue(currentBatch)),
                    new SqlParameter("@PHYQTY", phyQty.Value),
                    new SqlParameter("@CUSRID", ToDbValue(userId)),
                    new SqlParameter("@LMUSRID", ToDbValue(userId)),
                    new SqlParameter("@PRCSDATE", prcsDate));

                TempData["SuccessMessage"] = "Saved Successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
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
