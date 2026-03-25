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

        private class MaterialBatchDetailItem
        {
            public int MTRLID { get; set; }
            public int STKBID { get; set; }
            public string BATCHNO { get; set; }
            public DateTime? STKEDATE { get; set; }
        }

        [HttpGet]
        public JsonResult GetMaterialBatches(int mtrlid)
        {
            try
            {
                var dt = new DataTable();
                using (var conn = new SqlConnection(db.Database.Connection.ConnectionString))
                using (var cmd = new SqlCommand("pr_Pharmacy_Year_End_Material_Batch_Detail", conn))
                using (var da = new SqlDataAdapter(cmd))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@PMTRLID", mtrlid);
                    conn.Open();
                    da.Fill(dt);
                }

                var list = new List<MaterialBatchDetailItem>();
                foreach (DataRow r in dt.Rows)
                {
                    Func<string, bool> hasCol = (name) =>
                        r.Table != null && r.Table.Columns != null &&
                        r.Table.Columns.Cast<DataColumn>().Any(c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase));

                    Func<string, object> getCol = (name) =>
                    {
                        if (r.Table == null || r.Table.Columns == null) return null;
                        var col = r.Table.Columns.Cast<DataColumn>().FirstOrDefault(c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase));
                        return col != null ? r[col] : null;
                    };

                    object rawMtrlId = hasCol("MTRLID") ? getCol("MTRLID") : null;
                    object rawStkBid = hasCol("STKBID") ? getCol("STKBID") : null;
                    object rawBatchNo = hasCol("BATCHNO") ? getCol("BATCHNO") : (hasCol("BATCH") ? getCol("BATCH") : null);
                    object rawStkEdate = hasCol("STKEDATE") ? getCol("STKEDATE") : (hasCol("EXPIRYDATE") ? getCol("EXPIRYDATE") : (hasCol("EDATE") ? getCol("EDATE") : null));

                    var item = new MaterialBatchDetailItem();
                    try { item.MTRLID = rawMtrlId == null || rawMtrlId == DBNull.Value ? 0 : Convert.ToInt32(rawMtrlId); } catch { item.MTRLID = 0; }
                    try { item.STKBID = rawStkBid == null || rawStkBid == DBNull.Value ? 0 : Convert.ToInt32(rawStkBid); } catch { item.STKBID = 0; }
                    try { item.BATCHNO = rawBatchNo == null || rawBatchNo == DBNull.Value ? null : Convert.ToString(rawBatchNo); } catch { item.BATCHNO = null; }
                    try { item.STKEDATE = rawStkEdate == null || rawStkEdate == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rawStkEdate); } catch { item.STKEDATE = null; }
                    if (item.STKBID > 0 || !string.IsNullOrWhiteSpace(item.BATCHNO))
                    {
                        list.Add(item);
                    }
                }

                var data = list
                    .OrderByDescending(x => x.STKEDATE ?? DateTime.MinValue)
                    .ThenBy(x => x.BATCHNO)
                    .Select(x => new
                    {
                        mtrlid = x.MTRLID,
                        stkBid = x.STKBID,
                        batchNo = x.BATCHNO,
                        expiryDate = x.STKEDATE.HasValue ? x.STKEDATE.Value.ToString("yyyy-MM-dd") : string.Empty
                    })
                    .ToList();

                return Json(new { ok = true, data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message, data = new object[0] }, JsonRequestBehavior.AllowGet);
            }
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
                    var query = "SELECT * FROM (SELECT *, CAST(NULL AS int) AS TRANDREFID, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526 WHERE STKBID = 0 AND (MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p0)) AS T WHERE T.RowNum BETWEEN @p1 AND @p2";

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
                        "SELECT * FROM (SELECT *, CAST(NULL AS int) AS TRANDREFID, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM StockMaster_2526 WHERE STKBID = 0) AS T WHERE T.RowNum BETWEEN @p0 AND @p1",
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
                "SELECT TOP 1 *, CAST(NULL AS int) AS TRANDREFID FROM StockMaster_2526 WHERE SID = @p0", id).FirstOrDefault();

            if (row == null)
            {
                return HttpNotFound();
            }

            int selectedMtrlId = 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(row.MTRLDESC))
                {
                    selectedMtrlId = db.Database.SqlQuery<int>(
                            @"SELECT TOP 1 MTRLID
                              FROM MATERIALMASTER
                              WHERE MTRLDESC = @p0
                                AND (DISPSTATUS = 0 OR DISPSTATUS IS NULL)
                                AND (@p1 IS NULL OR MTRLGID = @p1)",
                            row.MTRLDESC,
                            row.MTRLGID)
                        .FirstOrDefault();
                }
            }
            catch
            {
                selectedMtrlId = 0;
            }

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

            if (string.IsNullOrWhiteSpace(currentBatch))
            {
                LoadAllMaterials(mtrlid);
                ViewBag.IsEdit = true;
                ViewBag.SID = SID;
                ViewBag.CurrentBatch = currentBatch;
                ViewBag.PhyQty = phyQty;
                ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                ViewBag.ErrorMessage = "Please enter Current Batch.";
                return View("Add");
            }

            if (!phyQty.HasValue)
            {
                LoadAllMaterials(mtrlid);
                ViewBag.IsEdit = true;
                ViewBag.SID = SID;
                ViewBag.CurrentBatch = currentBatch;
                ViewBag.PhyQty = phyQty;
                ViewBag.ExpiryDate = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                ViewBag.ErrorMessage = "Please enter Physical Quantity.";
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
    CURRENTBATCH = @p0,
    PHYQTY = @p1,
    BATCHNO = @p2,
    MTRLSTKQTY = @p3,
    STKEDATE = @p4,
    LMUSRID = @p5,
    PRCSDATE = @p6
WHERE SID = @p7",
                    ToDbValue(currentBatch),
                    ToDbValue(phyQty.Value),
                    ToDbValue(currentBatch),
                    ToDbValue(phyQty.Value),
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
        public JsonResult GetMaterialDetails(int mtrlid, int? stkBid = null)
        {
            try
            {
                var opening = GetTop1RowAsDictionary("Z_OPENING_SUPPLIER_HSN_DETAIL_ASSGN_001", mtrlid, stkBid);
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

        private Dictionary<string, object> GetTop1RowAsDictionary(string viewName, int mtrlid, int? stkBid = null)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string sql;
            if (stkBid.HasValue && stkBid.Value > 0)
            {
                sql = "SELECT TOP 1 * FROM " + viewName + " WHERE STKBID = @stkBid";
            }
            else
            {
                sql = "SELECT TOP 1 * FROM " + viewName + " WHERE MTRLID = @mtrlid";
            }

            var dt = new DataTable();
            using (var conn = new SqlConnection(db.Database.Connection.ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            using (var da = new SqlDataAdapter(cmd))
            {
                if (stkBid.HasValue && stkBid.Value > 0)
                {
                    cmd.Parameters.AddWithValue("@stkBid", stkBid.Value);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@mtrlid", mtrlid);
                }
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

                var insertSql =
                    @"INSERT INTO StockMaster_2526
(
    STKBID,
    TRANREFID,
    TRANREFNAME,
    TRANDREFGID,
    MTRLGID,
    MTRLGDESC,
    MTRLDESC,
    DACHEADID,
    PACKMID,
    BATCHNO,
    STKEDATE,
    MTRLSTKQTY,
    STKPRATE,
    STKMRP,
    ASTKSRATE,
    HSNID,
    TRANBCGSTEXPRN,
    TRANBSGSTEXPRN,
    TRANBIGSTEXPRN,
    TRANBCGSTAMT,
    TRANBSGSTAMT,
    TRANBIGSTAMT,
    CLVALUE,
    CURRENTBATCH,
    PHYQTY,
    CUSRID,
    LMUSRID,
    PRCSDATE
)
VALUES
(
    @p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16,@p17,@p18,@p19,@p20,@p21,@p22,@p23,@p24,@p25,@p26,@p27
)";

                db.Database.ExecuteSqlCommand(
                    insertSql,
                    stkBid,
                    tranRefId,
                    tranRefName,
                    trandRefGid,
                    ToDbValue(mtrlGid),
                    ToDbValue(mtrlGDesc),
                    ToDbValue(mtrlDesc),
                    dacheadId,
                    packMid,
                    batchNo ?? string.Empty,
                    stkeDate,
                    ToDbValue(mtrlStkQty),
                    stkPrate,
                    stkMrp,
                    astkSRate,
                    hsnId,
                    cgstExprn,
                    sgstExprn,
                    igstExprn,
                    cgstAmt,
                    sgstAmt,
                    igstAmt,
                    ToDbValue(clValue),
                    ToDbValue(currentBatch),
                    phyQty.Value,
                    ToDbValue(userId),
                    ToDbValue(userId),
                    prcsDate);

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
