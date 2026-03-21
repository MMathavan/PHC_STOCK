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
    public class SubStoreManualEntryController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        [HttpGet]
        [Route("SubStoreManualEntry")]
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
        [Route("SubStoreManualEntry/DownloadPdf")]
        public ActionResult DownloadPdf(int materialGroupId, string from, string to)
        {
            if (materialGroupId <= 0)
            {
                return RedirectToAction("Index", new { materialGroupId = (int?)null });
            }

            string materialGroupName = db.Database.SqlQuery<string>(
                    "SELECT TOP 1 MTRLGDESC FROM MATERIALGROUPMASTER WHERE MTRLGID = @p0",
                    materialGroupId)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(materialGroupName))
            {
                materialGroupName = "ManualEntry";
            }

            bool isTabletsGroup = string.Equals(materialGroupName.Trim(), "Tablets", StringComparison.OrdinalIgnoreCase);
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

            List<ManualEntryRow> data;
            if (isTabletsGroup && alphaFrom != null && alphaTo != null)
            {
                data = db.Database.SqlQuery<ManualEntryRow>(
                        @"SELECT 
                            MTRLDESC AS TRANREFNAME,
                            BATCHNO,
                            STKEDATE,
                            BATCHNO AS CURRENTBATCHNO,
                            MTRLSTKQTY AS PHYQTY
                        FROM SubStoreStockMaster_2526
                        WHERE ISNULL(STKBID, 0) <> 0
                          AND MTRLGID = @p0
                          AND UPPER(LEFT(ISNULL(MTRLDESC, ''), 1)) >= @p1
                          AND UPPER(LEFT(ISNULL(MTRLDESC, ''), 1)) <= @p2
                        ORDER BY MTRLDESC, BATCHNO, STKEDATE",
                        materialGroupId, alphaFrom, alphaTo)
                    .ToList();
            }
            else
            {
                data = db.Database.SqlQuery<ManualEntryRow>(
                        @"SELECT 
                            MTRLDESC AS TRANREFNAME,
                            BATCHNO,
                            STKEDATE,
                            BATCHNO AS CURRENTBATCHNO,
                            MTRLSTKQTY AS PHYQTY
                        FROM SubStoreStockMaster_2526
                        WHERE ISNULL(STKBID, 0) <> 0
                          AND MTRLGID = @p0
                        ORDER BY MTRLDESC, BATCHNO, STKEDATE",
                        materialGroupId)
                    .ToList();
            }

            string reportTitle = materialGroupName;
            if (isTabletsGroup)
            {
                reportTitle = materialGroupName.Trim();
                if (alphaFrom != null && alphaTo != null)
                {
                    reportTitle = string.Format("{0} from {1} to {2}", materialGroupName.Trim(), alphaFrom, alphaTo);
                }
            }

            byte[] pdfBytes = BuildManualEntryPdf(reportTitle, DateTime.Now, data);

            string fileTitle = materialGroupName;
            if (isTabletsGroup && alphaFrom != null && alphaTo != null)
            {
                fileTitle = string.Format("{0} from {1} to {2}", materialGroupName.Trim(), alphaFrom, alphaTo);
            }

            string safeFileName = MakeSafeFileName(fileTitle) + ".pdf";
            return File(pdfBytes, "application/pdf", safeFileName);
        }

        private static byte[] BuildManualEntryPdf(string title, DateTime printedAt, List<ManualEntryRow> rows)
        {
            const int rowsPerPage = 37;
            int totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)rowsPerPage));

            using (var ms = new MemoryStream())
            {
                var pageSize = PageSize.A4;
                using (var document = new Document(pageSize, 36f, 36f, 40f, 45f))
                {
                    var writer = PdfWriter.GetInstance(document, ms);
                    writer.PageEvent = new ManualEntryPdfPageEvent(totalPages);

                    document.Open();

                    var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.BLACK);
                    var fontDate = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);
                    var fontHeader = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, BaseColor.WHITE);
                    var fontCell = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);

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

                        var table = new PdfPTable(6) { WidthPercentage = 100 };
                        table.SetWidths(new float[] { 0.7f, 4.2f, 1.6f, 1.3f, 1.9f, 0.8f });
                        table.HeaderRows = 1;

                        var headerBg = new BaseColor(46, 117, 182);
                        table.AddCell(MakeTableHeaderCell("S.NO", fontHeader, headerBg, noWrap: true, align: Element.ALIGN_CENTER));
                        table.AddCell(MakeTableHeaderCell("TRANDREFNAME", fontHeader, headerBg));
                        table.AddCell(MakeTableHeaderCell("BATCHNO", fontHeader, headerBg));
                        table.AddCell(MakeTableHeaderCell("STKEDATE", fontHeader, headerBg));
                        table.AddCell(MakeTableHeaderCell("CURRENT BATCH NO", fontHeader, headerBg, noWrap: false, align: Element.ALIGN_CENTER));
                        table.AddCell(MakeTableHeaderCell("PHY.QTY", fontHeader, headerBg));

                        int index = (pageNo - 1) * rowsPerPage;
                        int rowCount = 0;
                        while (rowCount < rowsPerPage && index < rows.Count)
                        {
                            var r = rows[index];

                            int serialNo = index + 1;
                            table.AddCell(MakeTableCell(serialNo.ToString(), fontCell, Element.ALIGN_CENTER));
                            table.AddCell(MakeTableCell(r.TRANREFNAME, fontCell, Element.ALIGN_LEFT, noWrap: false));
                            table.AddCell(MakeTableCell(r.BATCHNO, fontCell, Element.ALIGN_LEFT));
                            table.AddCell(MakeTableCell(r.STKEDATE.HasValue ? r.STKEDATE.Value.ToString("dd-MMM-yy") : "", fontCell, Element.ALIGN_CENTER));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_RIGHT));

                            index++;
                            rowCount++;
                        }

                        if (rows.Count == 0)
                        {
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_CENTER));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_CENTER));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_RIGHT));
                            rowCount = 1;
                        }

                        bool isLastPage = pageNo == totalPages;
                        if (!isLastPage && rowCount < rowsPerPage)
                        {
                            for (int i = rowCount; i < rowsPerPage; i++)
                            {
                                table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_CENTER));
                                table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                                table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                                table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_CENTER));
                                table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                                table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_RIGHT));
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
                FixedHeight = 24f,
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
                FixedHeight = 16f,
                PaddingTop = 4f,
                PaddingBottom = 4f,
                PaddingLeft = 4f,
                PaddingRight = 4f,
                BorderWidth = 0.6f
            };
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "ManualEntry";
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

        private class ManualEntryRow
        {
            public string TRANREFNAME { get; set; }
            public string BATCHNO { get; set; }
            public DateTime? STKEDATE { get; set; }
            public string CURRENTBATCHNO { get; set; }
            public decimal? PHYQTY { get; set; }
        }

        private class ManualEntryPdfPageEvent : PdfPageEventHelper
        {
            private readonly int totalPages;
            private readonly Font footerFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);

            public ManualEntryPdfPageEvent(int totalPages)
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
