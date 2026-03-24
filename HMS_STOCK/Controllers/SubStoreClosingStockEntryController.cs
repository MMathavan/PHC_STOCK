using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using HMS_STOCK.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace HMS_STOCK.Controllers
{
    public class SubStoreClosingStockEntryController : Controller
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
                    "SELECT ISNULL(SUM(MTRLSTKQTY), 0) FROM SubStoreStockMaster_2526 WHERE MTRLGID = @p0",
                    materialGroupId.Value).FirstOrDefault();

                totalValue = db.Database.SqlQuery<decimal>(
                    "SELECT ISNULL(SUM(CLVALUE), 0) FROM SubStoreStockMaster_2526 WHERE MTRLGID = @p0",
                    materialGroupId.Value).FirstOrDefault();
            }
            else
            {
                totalQty = db.Database.SqlQuery<decimal>(
                    "SELECT ISNULL(SUM(MTRLSTKQTY), 0) FROM SubStoreStockMaster_2526").FirstOrDefault();

                totalValue = db.Database.SqlQuery<decimal>(
                    "SELECT ISNULL(SUM(CLVALUE), 0) FROM SubStoreStockMaster_2526").FirstOrDefault();
            }

            ViewBag.MaterialGroups = new SelectList(materialGroups, "MTRLGID", "MTRLGDESC", materialGroupId);
            ViewBag.SelectedMaterialGroup = materialGroupId;
            ViewBag.TotalQuantity = totalQty;
            ViewBag.TotalValue = totalValue;
            ViewBag.ClosingStockEntrySearch = Session != null ? (Session["ClosingStockEntrySearch"] ?? string.Empty) : string.Empty;

            return View(new List<SubStoreStockMaster_2526>());
        }

        [HttpGet]
        public ActionResult DownloadPdf(int? materialGroupId, string search)
        {
            try
            {
                bool hasMaterialFilter = materialGroupId.HasValue && materialGroupId.Value > 0;
                bool hasSearch = !string.IsNullOrWhiteSpace(search);

                string where = "WHERE ISNULL(STKBID, 0) <> 0";
                var parameters = new List<object>();

                if (hasMaterialFilter)
                {
                    where += " AND MTRLGID = @p0";
                    parameters.Add(materialGroupId.Value);
                }

                if (hasSearch)
                {
                    var idx = parameters.Count;
                    where += " AND (MTRLDESC LIKE @p" + idx + " OR BATCHNO LIKE @p" + idx + " OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p" + idx + ")";
                    parameters.Add("%" + search.Trim() + "%");
                }

                var query = @"SELECT
                        STKBID,
                        TRANREFID,
                        TRANREFNAME,
                        TRANDREFGID,
                        MTRLGID,
                        TRANDREFID,
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
                        CLVALUE
                    FROM SubStoreStockMaster_2526 " + where + @"
                    ORDER BY MTRLGDESC, MTRLDESC, BATCHNO, STKEDATE";

                var rows = db.Database.SqlQuery<SubStoreStockMaster_2526>(query, parameters.ToArray()).ToList();
                var pdfBytes = BuildClosingStockEntryPdf(rows);
                return File(pdfBytes, "application/pdf", "CLOSING_STOCK_2026-2027.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", new { materialGroupId = materialGroupId });
            }
        }

        private static byte[] BuildClosingStockEntryPdf(List<SubStoreStockMaster_2526> rows)
        {
            using (var ms = new MemoryStream())
            {
                var pageSize = PageSize.A4.Rotate();
                using (var document = new Document(pageSize, 16f, 16f, 22f, 22f))
                {
                    PdfWriter.GetInstance(document, ms);
                    document.Open();

                    var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.BLACK);
                    var fontHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 6.5f, BaseColor.WHITE);
                    var fontCell = FontFactory.GetFont(FontFactory.HELVETICA, 6.5f, BaseColor.BLACK);

                    var title = new Paragraph("CLOSING STOCK 2026 - 2027", fontTitle)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 8f
                    };
                    document.Add(title);

                    var headers = new string[]
                    {
                        "STKBID","TRANREFID","TRANREFNAME","TRANDREFGID","MTRLGID","TRANDREFID",
                        "MTRLGDESC","MTRLDESC","DACHEADID","PACKMID","BATCHNO","STKEDATE",
                        "MTRLSTKQTY","STKPRATE","STKMRP","ASTKSRATE","HSNID","TRANBCGSTEXPRN",
                        "TRANBSGSTEXPRN","TRANBIGSTEXPRN","TRANBCGSTAMT","TRANBSGSTAMT","TRANBIGSTAMT","CLVALUE"
                    };

                    var table = new PdfPTable(headers.Length)
                    {
                        WidthPercentage = 100,
                        SplitLate = false
                    };
                    table.HeaderRows = 1;

                    var headerBg = new BaseColor(46, 180, 255);
                    for (int i = 0; i < headers.Length; i++)
                    {
                        table.AddCell(new PdfPCell(new Phrase(headers[i], fontHeader))
                        {
                            BackgroundColor = headerBg,
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 3f,
                            BorderWidth = 0.5f,
                            NoWrap = false
                        });
                    }

                    if (rows == null) rows = new List<SubStoreStockMaster_2526>();
                    if (rows.Count == 0)
                    {
                        for (int i = 0; i < headers.Length; i++)
                        {
                            table.AddCell(new PdfPCell(new Phrase(string.Empty, fontCell)) { Padding = 3f, BorderWidth = 0.4f });
                        }
                    }
                    else
                    {
                        foreach (var r in rows)
                        {
                            table.AddCell(MakeCell(r.STKBID.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANREFID.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANREFNAME, fontCell));
                            table.AddCell(MakeCell(r.TRANDREFGID.ToString(), fontCell));
                            table.AddCell(MakeCell(r.MTRLGID?.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANDREFID?.ToString(), fontCell));
                            table.AddCell(MakeCell(r.MTRLGDESC, fontCell));
                            table.AddCell(MakeCell(r.MTRLDESC, fontCell));
                            table.AddCell(MakeCell(r.DACHEADID.ToString(), fontCell));
                            table.AddCell(MakeCell(r.PACKMID.ToString(), fontCell));
                            table.AddCell(MakeCell(r.BATCHNO, fontCell));
                            table.AddCell(MakeCell(r.STKEDATE.ToString("yyyy-MM-dd"), fontCell));
                            table.AddCell(MakeCell(r.MTRLSTKQTY?.ToString(), fontCell));
                            table.AddCell(MakeCell(r.STKPRATE.ToString(), fontCell));
                            table.AddCell(MakeCell(r.STKMRP.ToString(), fontCell));
                            table.AddCell(MakeCell(r.ASTKSRATE.ToString(), fontCell));
                            table.AddCell(MakeCell(r.HSNID.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANBCGSTEXPRN.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANBSGSTEXPRN.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANBIGSTEXPRN.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANBCGSTAMT.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANBSGSTAMT.ToString(), fontCell));
                            table.AddCell(MakeCell(r.TRANBIGSTAMT.ToString(), fontCell));
                            table.AddCell(MakeCell(r.CLVALUE?.ToString(), fontCell));
                        }
                    }

                    document.Add(table);
                    document.Close();
                }
                return ms.ToArray();
            }
        }

        private static PdfPCell MakeCell(string text, Font font)
        {
            return new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 3f,
                BorderWidth = 0.4f,
                NoWrap = false
            };
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
                    "SELECT COUNT(*) FROM SubStoreStockMaster_2526 WHERE ISNULL(STKBID, 0) <> 0").FirstOrDefault();

                int filteredCount = totalCount;
                List<SubStoreStockMaster_2526> stockData;

                bool hasMaterialFilter = materialGroupId.HasValue && materialGroupId.Value > 0;
                bool hasSearch = !string.IsNullOrWhiteSpace(searchValue);

                if (hasMaterialFilter)
                {
                    string where = "WHERE ISNULL(STKBID, 0) <> 0 AND MTRLGID = @p0";
                    if (hasSearch)
                    {
                        where += " AND (MTRLDESC LIKE @p1 OR BATCHNO LIKE @p1 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p1)";
                    }

                    var query = "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM SubStoreStockMaster_2526 " + where + ") AS T WHERE T.RowNum BETWEEN @p" + (hasSearch ? "2" : "1") + " AND @p" + (hasSearch ? "3" : "2") + ";";

                    if (hasSearch)
                    {
                        stockData = db.Database.SqlQuery<SubStoreStockMaster_2526>(
                            query,
                            materialGroupId.Value, "%" + searchValue + "%", startRowNum, endRowNum).ToList();

                        filteredCount = db.Database.SqlQuery<int>(
                            "SELECT COUNT(*) FROM SubStoreStockMaster_2526 " + where,
                            materialGroupId.Value, "%" + searchValue + "%").FirstOrDefault();
                    }
                    else
                    {
                        stockData = db.Database.SqlQuery<SubStoreStockMaster_2526>(
                            query,
                            materialGroupId.Value, startRowNum, endRowNum).ToList();

                        filteredCount = db.Database.SqlQuery<int>(
                            "SELECT COUNT(*) FROM SubStoreStockMaster_2526 " + where,
                            materialGroupId.Value).FirstOrDefault();
                    }
                }
                else if (hasSearch)
                {
                    var query = "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM SubStoreStockMaster_2526 WHERE ISNULL(STKBID, 0) <> 0 AND (MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p0)) AS T WHERE T.RowNum BETWEEN @p1 AND @p2";

                    stockData = db.Database.SqlQuery<SubStoreStockMaster_2526>(
                        query,
                        "%" + searchValue + "%", startRowNum, endRowNum).ToList();

                    filteredCount = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(*) FROM SubStoreStockMaster_2526
                          WHERE ISNULL(STKBID, 0) <> 0 AND (MTRLDESC LIKE @p0 OR BATCHNO LIKE @p0 OR CONVERT(varchar(10), STKEDATE, 23) LIKE @p0)",
                        "%" + searchValue + "%").FirstOrDefault();
                }
                else
                {
                    stockData = db.Database.SqlQuery<SubStoreStockMaster_2526>(
                        "SELECT * FROM (SELECT *, ROW_NUMBER() OVER (ORDER BY " + sortColumnName + " " + sortDirection + ") AS RowNum FROM SubStoreStockMaster_2526 WHERE ISNULL(STKBID, 0) <> 0) AS T WHERE T.RowNum BETWEEN @p0 AND @p1",
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
                    data = new List<SubStoreStockMaster_2526>(),
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
                    @"UPDATE SubStoreStockMaster_2526
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
                    @"UPDATE SubStoreStockMaster_2526
                      SET CURRENTBATCH = NULL,
                          PHYQTY = 0,
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
