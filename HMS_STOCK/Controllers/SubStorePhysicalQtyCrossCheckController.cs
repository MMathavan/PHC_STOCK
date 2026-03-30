using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using HMS_STOCK.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace HMS_STOCK.Controllers
{
    [SessionExpire]
    [RoutePrefix("Reports")]
    public class SubStorePhysicalQtyCrossCheckController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        [HttpGet]
        [Route("SubStorePhysicalQtyCrossCheck")]
        public ActionResult Index(int? materialGroupId)
        {
            var materialGroups = db.Database.SqlQuery<MaterialGroupMaster>(
                    "SELECT MTRLGID, MTRLGDESC FROM MATERIALGROUPMASTER WHERE MTRLTID = 2 AND DISPSTATUS = 0 ORDER BY MTRLGDESC")
                .ToList();

            ViewBag.MaterialGroups = new SelectList(materialGroups, "MTRLGID", "MTRLGDESC", materialGroupId);
            ViewBag.SelectedMaterialGroup = materialGroupId;

            return View();
        }

        [HttpGet]
        [Route("SubStorePhysicalQtyCrossCheck/DownloadPdf")]
        public ActionResult DownloadPdf(int? materialGroupId, string from, string to)
        {
            string materialGroupName = null;
            if (materialGroupId.HasValue && materialGroupId.Value > 0)
            {
                materialGroupName = db.Database.SqlQuery<string>(
                        "SELECT TOP 1 MTRLGDESC FROM MATERIALGROUPMASTER WHERE MTRLGID = @p0",
                        materialGroupId.Value)
                    .FirstOrDefault();
            }

            bool isTabletsGroup = string.Equals((materialGroupName ?? string.Empty).Trim(), "Tablets", StringComparison.OrdinalIgnoreCase);

            string alphaFrom = null;
            string alphaTo = null;
            if (isTabletsGroup && !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
            {
                alphaFrom = from.Trim().ToUpperInvariant();
                alphaTo = to.Trim().ToUpperInvariant();

                if (alphaFrom.Length != 1 || alphaTo.Length != 1)
                {
                    alphaFrom = null;
                    alphaTo = null;
                }
                else
                {
                    char cf = alphaFrom[0];
                    char ct = alphaTo[0];
                    if (cf < 'A' || cf > 'Z' || ct < 'A' || ct > 'Z' || cf > ct)
                    {
                        alphaFrom = null;
                        alphaTo = null;
                    }
                }
            }

            var sql = @"SELECT *
FROM VW_NEW_DRUG_SUBSTORE_STOCK_2627
WHERE 1 = 1";

            var sqlParams = new List<SqlParameter>();

            if (materialGroupId.HasValue && materialGroupId.Value > 0)
            {
                sql += " AND MTRLGID = @MTRLGID";
                sqlParams.Add(new SqlParameter("@MTRLGID", materialGroupId.Value));

                if (isTabletsGroup && alphaFrom != null && alphaTo != null)
                {
                    sql += " AND UPPER(LEFT(ISNULL(MTRLDESC, ''), 1)) >= @AlphaFrom";
                    sqlParams.Add(new SqlParameter("@AlphaFrom", alphaFrom));

                    sql += " AND UPPER(LEFT(ISNULL(MTRLDESC, ''), 1)) <= @AlphaTo";
                    sqlParams.Add(new SqlParameter("@AlphaTo", alphaTo));
                }
            }

            var data = ExecuteToDataTable(sql, sqlParams);

            string title = "SUB STORE PHYSICAL QTY CROSS CHECK";
            if (!string.IsNullOrWhiteSpace(materialGroupName))
            {
                title = "SUB STORE " + materialGroupName.Trim() + " PHYSICAL QTY CROSS CHECK";
                if (isTabletsGroup && alphaFrom != null && alphaTo != null)
                {
                    title = "SUB STORE " + materialGroupName.Trim() + " (" + alphaFrom + " - " + alphaTo + ") PHYSICAL QTY CROSS CHECK";
                }
            }

            var pdfBytes = BuildPdf(title, DateTime.Now, data);
            string safeFileName = MakeSafeFileName(title) + ".pdf";
            return File(pdfBytes, "application/pdf", safeFileName);
        }

        private static byte[] BuildPdf(string title, DateTime printedAt, DataTable data)
        {
            const int rowsPerPage = 22;
            int rowCountTotal = data != null ? data.Rows.Count : 0;
            int totalPages = Math.Max(1, (int)Math.Ceiling(rowCountTotal / (double)rowsPerPage));

            using (var ms = new MemoryStream())
            {
                var pageSize = PageSize.A4;
                using (var document = new Document(pageSize.Rotate(), 18f, 18f, 30f, 38f))
                {
                    var writer = PdfWriter.GetInstance(document, ms);
                    writer.PageEvent = new PdfPageEvent(totalPages);

                    document.Open();

                    var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.BLACK);
                    var fontDate = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);
                    var fontHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7, BaseColor.WHITE);
                    var fontCell = FontFactory.GetFont(FontFactory.HELVETICA, 7, BaseColor.BLACK);

                    for (int pageNo = 1; pageNo <= totalPages; pageNo++)
                    {
                        if (pageNo > 1)
                        {
                            document.NewPage();
                        }

                        var headerTable = new PdfPTable(3) { WidthPercentage = 100 };
                        headerTable.SetWidths(new float[] { 1f, 2f, 1f });

                        headerTable.AddCell(MakeHeaderCell("", fontDate, Element.ALIGN_LEFT, Rectangle.NO_BORDER));
                        headerTable.AddCell(MakeHeaderCell(title.ToUpperInvariant(), fontTitle, Element.ALIGN_CENTER, Rectangle.NO_BORDER));
                        headerTable.AddCell(MakeHeaderCell(printedAt.ToString("dd-MM-yyyyHH:mm"), fontDate, Element.ALIGN_RIGHT, Rectangle.NO_BORDER));
                        document.Add(headerTable);

                        document.Add(new Paragraph(" "));

                        var columns = GetPrintableColumns(data);
                        int colCount = columns.Count;

                        var table = new PdfPTable(colCount + 1) { WidthPercentage = 100 };

                        var widths = new float[colCount + 1];
                        widths[0] = 0.55f;
                        for (int i = 1; i < widths.Length; i++) widths[i] = 1f;
                        table.SetWidths(widths);
                        table.HeaderRows = 1;

                        var headerBg = new BaseColor(46, 117, 182);
                        table.AddCell(MakeTableHeaderCell("S.NO", fontHeader, headerBg, noWrap: true, align: Element.ALIGN_CENTER));
                        foreach (var c in columns)
                        {
                            table.AddCell(MakeTableHeaderCell(c.ColumnName, fontHeader, headerBg, noWrap: false));
                        }

                        int index = (pageNo - 1) * rowsPerPage;
                        int rowCount = 0;
                        while (rowCount < rowsPerPage && index < rowCountTotal)
                        {
                            int serialNo = index + 1;
                            table.AddCell(MakeTableCell(serialNo.ToString(), fontCell, Element.ALIGN_CENTER));

                            var row = data.Rows[index];
                            foreach (var c in columns)
                            {
                                table.AddCell(MakeTableCell(FormatCellValue(row[c]), fontCell, Element.ALIGN_LEFT, noWrap: false));
                            }

                            index++;
                            rowCount++;
                        }

                        if (rowCountTotal == 0)
                        {
                            for (int i = 0; i < colCount + 1; i++)
                            {
                                table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT, noWrap: false));
                            }
                            rowCount = 1;
                        }

                        bool isLastPage = pageNo == totalPages;
                        if (!isLastPage && rowCount < rowsPerPage)
                        {
                            for (int i = rowCount; i < rowsPerPage; i++)
                            {
                                for (int c = 0; c < colCount + 1; c++)
                                {
                                    table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT, noWrap: false));
                                }
                            }
                        }

                        document.Add(table);
                    }

                    document.Close();
                }

                return ms.ToArray();
            }
        }

        private static PdfPCell MakeHeaderCell(string text, Font font, int align, int border)
        {
            return new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                HorizontalAlignment = align,
                Border = border,
                Padding = 0f
            };
        }

        private static PdfPCell MakeTableHeaderCell(string text, Font font, BaseColor bg, bool noWrap = false, int align = Element.ALIGN_LEFT)
        {
            return new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                BackgroundColor = bg,
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                NoWrap = noWrap,
                PaddingTop = 4f,
                PaddingBottom = 4f,
                PaddingLeft = 4f,
                BorderWidth = 0.8f
            };
        }

        private static PdfPCell MakeTableCell(string text, Font font, int align, bool noWrap = false)
        {
            return new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                NoWrap = noWrap,
                PaddingTop = 4f,
                PaddingBottom = 4f,
                PaddingLeft = 4f,
                PaddingRight = 4f,
                BorderWidth = 0.6f
            };
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "PhysicalQtyCrossCheck";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c.ToString(), "_");
            }
            return name;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private DataTable ExecuteToDataTable(string sql, List<SqlParameter> parameters)
        {
            var dt = new DataTable();

            var conn = db.Database.Connection;
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandType = CommandType.Text;
                if (parameters != null)
                {
                    foreach (var p in parameters) cmd.Parameters.Add(p);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
            }

            return dt;
        }

        private static List<DataColumn> GetPrintableColumns(DataTable dt)
        {
            var cols = new List<DataColumn>();
            if (dt == null) return cols;

            foreach (DataColumn c in dt.Columns)
            {
                var name = (c.ColumnName ?? string.Empty).Trim();
                if (name.Equals("CUSRID", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("LMUSRID", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("PRCSDATE", StringComparison.OrdinalIgnoreCase)) continue;
                cols.Add(c);
            }

            return cols;
        }

        private static string FormatCellValue(object value)
        {
            if (value == null || value == DBNull.Value) return string.Empty;

            if (value is DateTime dt) return dt.ToString("dd-MMM-yy");
            if (value is DateTimeOffset dto) return dto.DateTime.ToString("dd-MMM-yy");

            return Convert.ToString(value);
        }

        private class PdfPageEvent : PdfPageEventHelper
        {
            private readonly int totalPages;
            private readonly Font footerFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);

            public PdfPageEvent(int totalPages)
            {
                this.totalPages = totalPages;
            }

            public override void OnEndPage(PdfWriter writer, Document document)
            {
                base.OnEndPage(writer, document);

                var pageText = string.Format("Page {0} of {1}", writer.PageNumber, totalPages);

                var cb = writer.DirectContent;
                ColumnText.ShowTextAligned(
                    cb,
                    Element.ALIGN_CENTER,
                    new Phrase(pageText, footerFont),
                    (document.Left + document.Right) / 2,
                    document.Bottom - 15,
                    0);
            }
        }
    }
}
