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
using System.Linq;
using System.Web;
using System.Web.Mvc;

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

        public ActionResult Index()
        {
            try
            {
                var dt = new DataTable();
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

                return View(dt);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = ex.Message;
                return View(new DataTable());
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