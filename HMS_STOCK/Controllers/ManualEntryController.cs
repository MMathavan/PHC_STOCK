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
    public class ManualEntryController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();

        [HttpGet]
        [Route("ManualEntry")]
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
        [Route("ManualEntry/DownloadPdf")]
        public ActionResult DownloadPdf(int materialGroupId)
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

            var data = db.Database.SqlQuery<ManualEntryRow>(
                    @"SELECT 
                        TRANREFNAME,
                        BATCHNO,
                        STKEDATE,
                        BATCHNO AS CURRENTBATCHNO,
                        MTRLSTKQTY AS PHYQTY
                    FROM StockMaster_2526
                    WHERE MTRLGID = @p0
                    ORDER BY TRANREFNAME, BATCHNO, STKEDATE",
                    materialGroupId)
                .ToList();

            byte[] pdfBytes = BuildManualEntryPdf(materialGroupName, DateTime.Now, data);

            string safeFileName = MakeSafeFileName(materialGroupName) + ".pdf";
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

                        var table = new PdfPTable(5) { WidthPercentage = 100 };
                        table.SetWidths(new float[] { 4.6f, 1.7f, 1.4f, 2.0f, 0.9f });
                        table.HeaderRows = 1;

                        var headerBg = new BaseColor(46, 117, 182);
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

                            table.AddCell(MakeTableCell(r.TRANREFNAME, fontCell, Element.ALIGN_LEFT, noWrap: false));
                            table.AddCell(MakeTableCell(r.BATCHNO, fontCell, Element.ALIGN_LEFT));
                            table.AddCell(MakeTableCell(r.STKEDATE.HasValue ? r.STKEDATE.Value.ToString("dd-MMM-yy") : "", fontCell, Element.ALIGN_CENTER));
                            table.AddCell(MakeTableCell(string.Empty, fontCell, Element.ALIGN_LEFT));
                            table.AddCell(MakeTableCell(r.PHYQTY.HasValue ? r.PHYQTY.Value.ToString("0.##") : "", fontCell, Element.ALIGN_RIGHT));

                            index++;
                            rowCount++;
                        }

                        if (rows.Count == 0)
                        {
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
