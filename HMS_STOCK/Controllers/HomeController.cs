using HMS_STOCK.Data;
using HMS_STOCK.Filters;
using HMS_STOCK.Models;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using log4net;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace HMS_STOCK.Controllers
{

    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        //private static readonly ILog log = LogManager.GetLogger(typeof(MembersController));

        public HomeController()
        {
            _db = new ApplicationDbContext();
        }

        private bool IsDashboardUser()
        {
            if (!Request.IsAuthenticated)
            {
                return false;
            }

            if (User != null && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("Manager")))
            {
                return true;
            }

            var session = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Session : null;
            var group = session != null ? Convert.ToString(session["Group"]) : null;
            return string.Equals(group, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(group, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(group, "Manager", StringComparison.OrdinalIgnoreCase);
        }

        public sealed class StockEntrySummaryRow
        {
            public string MTRLGDESC { get; set; }
            public int CNT { get; set; }
        }


        public ActionResult AdminDashboard()
        {
            // Dashboard accessible to all users (Admin and regular users)
            try
            {
                var statsDict = new Dictionary<string, DashboardStat>();

                System.Diagnostics.Debug.WriteLine("=== Dashboard Data Loading Started ===");


                ViewBag.DashboardStats = statsDict;

                System.Diagnostics.Debug.WriteLine("=== Dashboard Data Loading Completed Successfully ===");

                System.Diagnostics.Debug.WriteLine($"Dashboard stats loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR loading dashboard stats: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                ViewBag.DashboardStats = new Dictionary<string, DashboardStat>();
                ViewBag.ShrimpByType = new List<ShrimpByTypeDTO>();
                ViewBag.MonthlyInvoices = new List<MonthlyInvoiceDTO>();
                ViewBag.TopShrimpTypes = new List<TopShrimpTypeDTO>();
                ViewBag.ErrorMessage = ex.Message;
            }

            return View();
        }

        public ActionResult Index(int? physicalMaterialGroupId)
        {
            ViewBag.IsDashboardUser = IsDashboardUser();

            try
            {
                var connectionString = _db.Database.Connection.ConnectionString;

                if (!(bool)ViewBag.IsDashboardUser)
                {
                    var materialGroups = new List<SelectListItem>();
                    using (var conn = new SqlConnection(connectionString))
                    using (var cmd = new SqlCommand("SELECT MTRLGID, MTRLGDESC FROM MATERIALGROUPMASTER ORDER BY MTRLGDESC", conn))
                    {
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                materialGroups.Add(new SelectListItem
                                {
                                    Value = Convert.ToString(reader["MTRLGID"]),
                                    Text = Convert.ToString(reader["MTRLGDESC"])
                                });
                            }
                        }
                    }

                    ViewBag.PhysicalMaterialGroups = new SelectList(materialGroups, "Value", "Text", physicalMaterialGroupId);
                    ViewBag.SelectedPhysicalMaterialGroupId = physicalMaterialGroupId;

                    var physicalWhere = "STKBID <> 0";
                    if (physicalMaterialGroupId.HasValue)
                    {
                        physicalWhere += " AND MTRLGID = @p0";
                    }
                    var physicalSql = $@"SELECT ISNULL(MTRLGDESC, '') AS MTRLGDESC, COUNT(1) AS CNT
FROM StockMaster_2526
WHERE {physicalWhere}
  AND ISNULL(LTRIM(RTRIM(CURRENTBATCH)), '') <> ''
  AND PHYQTY IS NOT NULL
GROUP BY ISNULL(MTRLGDESC, '')
ORDER BY ISNULL(MTRLGDESC, '')";

                    var manualSql = @"SELECT ISNULL(MTRLGDESC, '') AS MTRLGDESC, COUNT(1) AS CNT
FROM StockMaster_2526
WHERE STKBID = 0
  AND ISNULL(LTRIM(RTRIM(CURRENTBATCH)), '') <> ''
  AND PHYQTY IS NOT NULL
GROUP BY ISNULL(MTRLGDESC, '')
ORDER BY ISNULL(MTRLGDESC, '')";

                    List<StockEntrySummaryRow> physicalSummary;
                    if (physicalMaterialGroupId.HasValue)
                    {
                        physicalSummary = _db.Database.SqlQuery<StockEntrySummaryRow>(physicalSql, physicalMaterialGroupId.Value).ToList();
                    }
                    else
                    {
                        physicalSummary = _db.Database.SqlQuery<StockEntrySummaryRow>(physicalSql).ToList();
                    }

                    var manualSummary = _db.Database.SqlQuery<StockEntrySummaryRow>(manualSql).ToList();

                    ViewBag.PhysicalSummary = physicalSummary;
                    ViewBag.ManualSummary = manualSummary;

                    return View(new DataTable());
                }

                var dt = new DataTable();
                var subStoreDt = new DataTable();

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("pr_Dashboard_Physical_Stock_Assgn", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("pr_Substore_Dashboard_Physical_Stock_Assgn", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(subStoreDt);
                    }
                }

                ViewBag.SubStoreDashboardTable = subStoreDt;

                return View(dt);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = ex.Message;
                return View(new DataTable());
            }
        }

        [HttpGet]
        public ActionResult DownloadDashboardPdf()
        {
            if (!IsDashboardUser())
            {
                return new HttpUnauthorizedResult();
            }

            try
            {
                var dt = new DataTable();
                var subStoreDt = new DataTable();
                var connectionString = _db.Database.Connection.ConnectionString;

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("pr_Dashboard_Physical_Stock_Assgn", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("pr_Substore_Dashboard_Physical_Stock_Assgn", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(subStoreDt);
                    }
                }

                byte[] pdfBytes = BuildDashboardPdf(dt, subStoreDt);
                return File(pdfBytes, "application/pdf", "PHARMACY_OPENING_CLOSING_STOCK_26-27.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        private static byte[] BuildDashboardPdf(DataTable dt, DataTable subStoreDt)
        {
            using (var ms = new MemoryStream())
            {
                var pageSize = PageSize.A4.Rotate();
                using (var document = new Document(pageSize, 24f, 24f, 30f, 30f))
                {
                    PdfWriter.GetInstance(document, ms);
                    document.Open();

                    var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.BLACK);
                    var fontHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, BaseColor.WHITE);
                    var fontCell = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);

                    void AddDashboardTable(DataTable data, string heading)
                    {
                        var title = new Paragraph(heading, fontTitle)
                        {
                            Alignment = Element.ALIGN_CENTER,
                            SpacingAfter = 10f
                        };
                        document.Add(title);

                        if (data == null || data.Columns == null || data.Columns.Count == 0)
                        {
                            document.Add(new Paragraph("No data available", fontCell));
                            document.Add(new Paragraph(" ", fontCell));
                            return;
                        }

                        var table = new PdfPTable(data.Columns.Count) { WidthPercentage = 100 };
                        table.HeaderRows = 1;

                        var headerBg = new BaseColor(25, 118, 210);
                        for (int i = 0; i < data.Columns.Count; i++)
                        {
                            string colName = data.Columns[i].ColumnName;
                            string headerText = colName;
                            if (string.Equals(colName, "MTRLGDESC", StringComparison.OrdinalIgnoreCase)) headerText = "Material Group Description (MTRLGDESC)";
                            else if (string.Equals(colName, "CLVALUE", StringComparison.OrdinalIgnoreCase)) headerText = "Closing Value (CLVALUE) 29-03-2026";
                            else if (string.Equals(colName, "OPVALUE", StringComparison.OrdinalIgnoreCase)) headerText = "Opening Value (OPVALUE) 01-04-2026";

                            var cell = new PdfPCell(new Phrase(headerText, fontHeader))
                            {
                                BackgroundColor = headerBg,
                                HorizontalAlignment = Element.ALIGN_CENTER,
                                VerticalAlignment = Element.ALIGN_MIDDLE,
                                Padding = 5f,
                                BorderWidth = 0.6f
                            };
                            table.AddCell(cell);
                        }

                        for (int r = 0; r < data.Rows.Count; r++)
                        {
                            var row = data.Rows[r];
                            bool isLastRow = r == data.Rows.Count - 1;
                            for (int c = 0; c < data.Columns.Count; c++)
                            {
                                string colName = data.Columns[c].ColumnName;
                                string text = row[c] == DBNull.Value ? string.Empty : Convert.ToString(row[c]);

                                BaseColor bg = BaseColor.WHITE;
                                BaseColor fg = BaseColor.BLACK;
                                if (string.Equals(colName, "EXCESS", StringComparison.OrdinalIgnoreCase))
                                {
                                    bg = new BaseColor(232, 245, 233);
                                    fg = new BaseColor(27, 94, 32);
                                }
                                else if (string.Equals(colName, "SHORT", StringComparison.OrdinalIgnoreCase))
                                {
                                    bg = new BaseColor(255, 235, 238);
                                    fg = new BaseColor(183, 28, 28);
                                }
                                else if (isLastRow)
                                {
                                    bg = new BaseColor(227, 242, 253);
                                }

                                var dataCell = new PdfPCell(new Phrase(text, fontCell))
                                {
                                    BackgroundColor = bg,
                                    HorizontalAlignment = Element.ALIGN_CENTER,
                                    VerticalAlignment = Element.ALIGN_MIDDLE,
                                    Padding = 4f,
                                    BorderWidth = 0.6f
                                };
                                dataCell.Phrase = new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, 8, fg));

                                if (isLastRow)
                                {
                                    dataCell.BorderWidth = 1.2f;
                                    dataCell.Phrase = new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, fg));
                                }

                                table.AddCell(dataCell);
                            }
                        }

                        document.Add(table);
                        document.Add(new Paragraph(" ", fontCell));
                    }

                    AddDashboardTable(dt, "PHARMACY OPENING CLOSING STOCK 2026 - 2027");
                    AddDashboardTable(subStoreDt, "Sub Store Opening Closing Stock 2026 - 2027");

                    document.Close();
                }

                return ms.ToArray();
            }
        }

        [HttpGet]
        public ActionResult RenewalPopup(int memberId)
        {
            return HttpNotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitRenewal(RenewalSubmitRequest request)
        {
            Response.StatusCode = 404;
            return Json(new { success = false, message = "Not available" });
        }

        [HttpGet]
        public ActionResult Notifications()
        {
            return HttpNotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptNotification(int eventId)
        {
            return new HttpStatusCodeResult(204);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeclineNotification(int eventId)
        {
            return new HttpStatusCodeResult(204);
        }

        [HttpGet]
        public ActionResult UserDashboard()
        {
            return HttpNotFound();
        }
    }

    // Dashboard DTOs
    public class DashboardStat
    {
        public string StatType { get; set; }
        public int TotalCount { get; set; }
        public string Details { get; set; }
    }

    public class ShrimpByTypeDTO
    {
        public string ReceivedType { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyInvoiceDTO
    {
        public string MonthName { get; set; }
        public int InvoiceCount { get; set; }
    }

    public class TopShrimpTypeDTO
    {
        public string ShrimpType { get; set; }
        public int Transactions { get; set; }
        public decimal TotalQuantity { get; set; }
    }
}