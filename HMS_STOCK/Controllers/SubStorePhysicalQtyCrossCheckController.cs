using System;
using System.Collections.Generic;
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

            var sql = @"SELECT
    ISNULL(MTRLGDESC, '') AS MTRLGDESC,
    ISNULL(MTRLDESC, '') AS MTRLDESC,
    ISNULL(BATCHNO, '') AS BATCHNO,
    STKEDATE,
    ISNULL(CURRENTBATCH, '') AS CURRENTBATCH,
    PHYQTY,
    EXPIRYDATE
FROM SubStoreStockMaster_2526
WHERE ISNULL(STKBID, 0) <> 0
  AND (PHYQTY IS NOT NULL AND PHYQTY > 0)";

            var parms = new List<object>();

            if (materialGroupId.HasValue && materialGroupId.Value > 0)
            {
                sql += " AND MTRLGID = @p" + parms.Count;
                parms.Add(materialGroupId.Value);

                if (isTabletsGroup && alphaFrom != null && alphaTo != null)
                {
                    sql += " AND UPPER(LEFT(ISNULL(MTRLDESC, ''), 1)) >= @p" + parms.Count;
                    parms.Add(alphaFrom);
                    sql += " AND UPPER(LEFT(ISNULL(MTRLDESC, ''), 1)) <= @p" + parms.Count;
                    parms.Add(alphaTo);
                }
            }

            sql += " ORDER BY ISNULL(MTRLGDESC, ''), ISNULL(MTRLDESC, ''), ISNULL(BATCHNO, ''), STKEDATE";

            var rows = db.Database.SqlQuery<PhysicalQtyCrossCheckRow>(sql, parms.ToArray()).ToList();

            string title = "SUB STORE PHYSICAL QTY CROSS CHECK";
            if (!string.IsNullOrWhiteSpace(materialGroupName))
            {
                title = "SUB STORE " + materialGroupName.Trim() + " PHYSICAL QTY CROSS CHECK";
                if (isTabletsGroup && alphaFrom != null && alphaTo != null)
                {
                    title = "SUB STORE " + materialGroupName.Trim() + " (" + alphaFrom + " - " + alphaTo + ") PHYSICAL QTY CROSS CHECK";
                }
            }

            var pdfBytes = BuildPdf(title, DateTime.Now, rows);
            string safeFileName = MakeSafeFileName(title) + ".pdf";
            return File(pdfBytes, "application/pdf", safeFileName);
        }

        private static byte[] BuildPdf(string title, DateTime printedAt, List<PhysicalQtyCrossCheckRow> rows)
        {
            const int rowsPerPage = 34;
            int totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)rowsPerPage));

            using (var ms = new MemoryStream())
            {
                var pageSize = PageSize.A4;
                using (var document = new Document(pageSize, 36f, 36f, 40f, 45f))
                {
                    var writer = PdfWriter.GetInstance(document, ms);
                    writer.PageEvent = new PdfPageEvent(totalPages);

                    document.Open();

                    var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.BLACK);
                    var fontDate = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                    var fontHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                    var fontCell = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);

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

                        var table = new PdfPTable(8) { WidthPercentage = 100 };
                        table.SetWidths(new float[] { 0.45f, 1f, 1.7f, 1.0f, 0.9f, 0.9f, 0.7f, 0.9f });
                        table.HeaderRows = 1;

                        var headerBg = new BaseColor(46, 117, 182);
                        table.AddCell(MakeTableHeaderCell("S.NO", fontHeader, headerBg, noWrap: true, align: Element.ALIGN_CENTER));
                        table.AddCell(MakeTableHeaderCell("MTRLGDESC", fontHeader, headerBg, noWrap: false));
                        table.AddCell(MakeTableHeaderCell("MTRLDESC", fontHeader, headerBg, noWrap: false));
                        table.AddCell(MakeTableHeaderCell("BATCHNO", fontHeader, headerBg, noWrap: false));
                        table.AddCell(MakeTableHeaderCell("STKEDATE", fontHeader, headerBg, noWrap: false, align: Element.ALIGN_CENTER));
                        table.AddCell(MakeTableHeaderCell("CURRENTBATCH", fontHeader, headerBg, noWrap: false));
                        table.AddCell(MakeTableHeaderCell("PHY.QTY", fontHeader, headerBg, noWrap: false, align: Element.ALIGN_RIGHT));
                        table.AddCell(MakeTableHeaderCell("EXPIRYDATE", fontHeader, headerBg, noWrap: false, align: Element.ALIGN_CENTER));

                        int index = (pageNo - 1) * rowsPerPage;
                        int rowCount = 0;
                        while (rowCount < rowsPerPage && index < rows.Count)
                        {
                            var r = rows[index];

                            int serialNo = index + 1;
                            table.AddCell(MakeTableCell(serialNo.ToString(), fontCell, Element.ALIGN_CENTER));
                            table.AddCell(MakeTableCell(r.MTRLGDESC, fontCell, Element.ALIGN_LEFT, noWrap: false));
                            table.AddCell(MakeTableCell(r.MTRLDESC, fontCell, Element.ALIGN_LEFT, noWrap: false));
                            table.AddCell(MakeTableCell(r.BATCHNO, fontCell, Element.ALIGN_LEFT, noWrap: false));
                            table.AddCell(MakeTableCell(r.STKEDATE.HasValue ? r.STKEDATE.Value.ToString("dd-MMM-yy") : "", fontCell, Element.ALIGN_CENTER, noWrap: false));
                            table.AddCell(MakeTableCell(r.CURRENTBATCH, fontCell, Element.ALIGN_LEFT, noWrap: false));
                            table.AddCell(MakeTableCell(r.PHYQTY.HasValue ? r.PHYQTY.Value.ToString("0.##") : "", fontCell, Element.ALIGN_RIGHT, noWrap: false));
                            table.AddCell(MakeTableCell(r.EXPIRYDATE.HasValue ? r.EXPIRYDATE.Value.ToString("dd-MMM-yy") : "", fontCell, Element.ALIGN_CENTER, noWrap: false));

                            index++;
                            rowCount++;
                        }

                        if (rows.Count == 0)
                        {
                            for (int i = 0; i < 8; i++)
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
                                for (int c = 0; c < 8; c++)
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

        private static PdfPCell MakeTableHeaderCell(string text, Font font, BaseColor bg, bool noWrap = true, int align = Element.ALIGN_LEFT)
        {
            return new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                BackgroundColor = bg,
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                NoWrap = noWrap,
                FixedHeight = 28f,
                PaddingTop = 4f,
                PaddingBottom = 4f,
                PaddingLeft = 4f,
                BorderWidth = 0.8f
            };
        }

        private static PdfPCell MakeTableCell(string text, Font font, int align, bool noWrap = true)
        {
            return new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                NoWrap = noWrap,
                FixedHeight = 20f,
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

        private class PhysicalQtyCrossCheckRow
        {
            public string MTRLGDESC { get; set; }
            public string MTRLDESC { get; set; }
            public string BATCHNO { get; set; }
            public DateTime? STKEDATE { get; set; }
            public string CURRENTBATCH { get; set; }
            public decimal? PHYQTY { get; set; }
            public DateTime? EXPIRYDATE { get; set; }
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
