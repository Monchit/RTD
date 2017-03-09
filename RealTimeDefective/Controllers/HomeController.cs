using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RealTimeDefective.Models;
using DotNet.Highcharts;
using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using System.Drawing;
using System.Globalization;
using WebCommonFunction;

namespace RealTimeDefective.Controllers
{
    public class HomeController : Controller
    {
        private TNCSecurity secure = new TNCSecurity();

        private defectiveliveEntities dbDef = new defectiveliveEntities();
        private qimnicsEntities dbQim = new qimnicsEntities();

        public ActionResult Index(string key = null)
        {
            if (key != null) return Login(key);
            else return View();
        }

        public ActionResult Login(string key = null)
        {
            string username = key == null ? Request.Form["Username"].ToString() : "";
            string pass = key == null ? Request.Form["Password"].ToString() : "";

            var chklogin = secure.Login(username, pass, false);//true:Real, false:Test

            if (key != null)
            {
                username = secure.WebCenterDecode(key);
                chklogin = secure.Login(username, "a", false);//set false only
            }

            if (chklogin != null)//Login Success
            {
                Session["RTD_Authen"] = chklogin.emp_code; // Employee ID
                Session["RTD_Name"] = chklogin.emp_fname + " " + chklogin.emp_lname;// FirstName & Lastname

                //var ulv = (from a in db.tm_user_lv
                //           where a.pos_min <= chklogin.position_level && a.pos_max >= chklogin.position_level
                //           select a).FirstOrDefault();

                //if (ulv != null)
                //{
                //    Session["RTD_Ulv"] = ulv.ulv_id;
                //    Session["RTD_Org"] = chklogin.LeafOrgGroupId;
                //    Session["RTD_Name"] = chklogin.emp_fname + " " + chklogin.emp_lname;// FirstName & Lastname
                //}

                //var chk_sys_user = db.tm_user.Find(chklogin.emp_code);

                //if (chk_sys_user != null)
                //{
                //    Session["RTD_USys"] = chk_sys_user.utype_id;
                //    Session["RTD_Plant"] = !string.IsNullOrEmpty(chk_sys_user.plant) ? chk_sys_user.plant : null;
                //    //return RedirectToAction("Index","Mold");
                //}
                //else
                //{
                //    Session["MCS_USys"] = 0;
                //}

                if (Session["RTD_Redirect"] != null)
                {
                    string url = Session["RTD_Redirect"].ToString();
                    Session.Remove("RTD_Redirect");
                    return Redirect(url);
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                //TempData["noty"] = "Username and/or Password is incorrect !!!";
                return RedirectToAction("Index", "Home");
            }
        }

        public ActionResult Logout()
        {
            Session.Remove("RTD_Authen"); // Check Login
            Session.Remove("RTD_Name"); // Show Name
            //Session.Remove("MCS_Org"); // Root Approve
            //Session.Remove("MCS_Ulv"); // Root Approve
            //Session.Remove("MCS_USys"); // Show Menu
            return RedirectToAction("Index", "Home");
        }
        
        [Chk_Authen]
        public ActionResult ByItem()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);

            return View();
        }

        public ActionResult _GetGraphByItem(string dtFrom, string dtTo, string fg)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 500
            })
            .SetTitle(new Title { Text = "Accumulate Defective by Item" })
            .SetYAxis(new YAxis
            {
                Min = 0,//Start at
                Title = new YAxisTitle { Text = "Percent Defective" }
            })
            //.SetTooltip(new Tooltip { 
            //    //Formatter = @"function() { return ''+ this.x +':'+ this.y +'%'; }", 
            //    ValueDecimals = 2
            //})
            .SetTooltip(new Tooltip 
            { 
                Enabled = false
                //PointFormat = "<b>{point.y:,.2f}</b>%" 
            })
            .SetPlotOptions(new PlotOptions
            {
                Spline = new PlotOptionsSpline
                {
                    Marker = new PlotOptionsSplineMarker
                    {
                        Enabled = false
                    }
                },
                Column = new PlotOptionsColumn
                {
                    DataLabels = new PlotOptionsColumnDataLabels { 
                        Enabled = true,
                        Formatter = "function() { return Highcharts.numberFormat(this.y, 2) + '%'; }" 
                    }
                }
            })
            .SetCredits(new Credits { Enabled = false });

            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];
            
            var get_target = dbDef.tm_defective_prod_code.Where(w => w.product_code == fg).Select(s => s.defective_rate).FirstOrDefault();

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       select new { item = a.eng_code, job = a.job_order_no, def = a.qty, prd = a.curing_qty };

            var get_def = from p in temp
                          group p by p.item into g
                          select new { item = g.Key, def = g.Sum(p => p.def) };
            
            var get_item = get_def.Select(s => s.item).ToArray();

            var count = get_def.Count();

            //object[] production = new object[count];
            //object[] defective = new object[count];
            object[] percent = new object[count];
            object[] target = new object[count];

            chart.SetXAxis(new XAxis
            {
                Categories = get_item,
                Title = new XAxisTitle { Text = "Item" },
                Labels = new XAxisLabels
                    {
                        Rotation = -45,
                        Align = HorizontalAligns.Right
                    }
            });

            int i = 0;
            foreach (var item in get_def)
            {
                if (item.def != null)
                {
                    var temp1 = temp.Where(w => w.item == item.item).Select(s => new { s.job, s.prd }).Distinct();
                    percent[i] = item.def * 100.0 / temp1.Sum(s => s.prd);
                    //production[i] = temp1.Sum(s => s.prd);
                    //defective[i] = item.def;
                }
                else
                {
                    //production[i] = 0;
                    //defective[i] = 0;
                    percent[i] = 0;
                }
                target[i] = get_target;
                i++;
            }

            //Data data = new Data(
            //    percent.Select(y => new DotNet.Highcharts.Options.Point { Color = GetBarColour(y), Y = y }).ToArray()
            //);

            chart.SetSeries(new[]
            {
                //new Series { Type = ChartTypes.Column, Name = "Production", Data = new Data(production) },
                new Series { Type = ChartTypes.Column, Name = "Def.", Data = new Data(percent) },
                new Series { Type = ChartTypes.Spline, Color = Color.Red, Name = "Target", Data = new Data(target) }
            });

            return PartialView(chart);
        }

        public JsonResult GetItemByFG(string fg)
        {
            if (string.IsNullOrEmpty(fg))
            {
                throw new ArgumentNullException("fg");
            }

            var result = (from a in dbQim.tm_master_item
                          where a.product_code == fg
                          select a.eng_code).Distinct().OrderBy(o => o);
                          //select new
                          //{
                          //    Value = a.eng_code,
                          //    Text = a.eng_code
                          //}).Distinct();

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult _GetGraphByItemDetail(string dtFrom, string dtTo, string fg, string item)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 500
            })
            .SetTitle(new Title { Text = "Chart Item by Date" })
            .SetYAxis(new YAxis
            {
                Min = 0,//Start at
                Title = new YAxisTitle { Text = "Percent Defective" }
            })
            //.SetTooltip(new Tooltip { 
            //    //Formatter = @"function() { return ''+ this.x +':'+ this.y +'%'; }", 
            //    ValueDecimals = 2
            //})
            .SetTooltip(new Tooltip
            {
                Enabled = false
                //PointFormat = "<b>{point.y:,.2f}</b>%" 
            })
            .SetPlotOptions(new PlotOptions
            {
                Spline = new PlotOptionsSpline
                {
                    Marker = new PlotOptionsSplineMarker
                    {
                        Enabled = false
                    }
                },
                Column = new PlotOptionsColumn
                {
                    Stacking = Stackings.Normal,
                    DataLabels = new PlotOptionsColumnDataLabels
                    {
                        Enabled = true,
                        Color = Color.White
                        //Formatter = "function() { return Highcharts.numberFormat(this.y, 2) + '%'; }"
                    }
                }
            })
            .SetCredits(new Credits { Enabled = false });

            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            DateTime date_from = DateTime.ParseExact(dfm, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime date_to = DateTime.ParseExact(dto, "yyyyMMdd", CultureInfo.InvariantCulture);

            double count = (date_to - date_from).TotalDays;

            object[] defective = new object[(int)count];

            for (DateTime i = date_from; DateTime.Compare(i, date_to) <= 0; i.AddDays(1))
            {
                
            }

            //var get_target = dbDef.tm_defective_prod_code.Where(w => w.product_code == fg).Select(s => s.defective_rate).FirstOrDefault();

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg && a.item == item &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       select new { item = a.eng_code, job = a.job_order_no, def = a.qty, prd = a.curing_qty };

            var get_def = from p in temp
                          group p by p.item into g
                          select new { item = g.Key, def = g.Sum(p => p.def) };

            var get_item = get_def.Select(s => s.item).ToArray();

            //var count = get_def.Count();

            //object[] production = new object[count];
            //object[] defective = new object[count];
            //object[] percent = new object[count];
            //object[] target = new object[count];

            chart.SetXAxis(new XAxis
            {
                Categories = get_item,
                Title = new XAxisTitle { Text = "Item" },
                Labels = new XAxisLabels
                {
                    Rotation = -45,
                    Align = HorizontalAligns.Right
                }
            });

            //int i = 0;
            //foreach (var item in get_def)
            //{
            //    if (item.def != null)
            //    {
            //        var temp1 = temp.Where(w => w.item == item.item).Select(s => new { s.job, s.prd }).Distinct();
            //        percent[i] = item.def * 100.0 / temp1.Sum(s => s.prd);
            //        //production[i] = temp1.Sum(s => s.prd);
            //        //defective[i] = item.def;
            //    }
            //    else
            //    {
            //        //production[i] = 0;
            //        //defective[i] = 0;
            //        percent[i] = 0;
            //    }
            //    target[i] = get_target;
            //    i++;
            //}

            //Data data = new Data(
            //    percent.Select(y => new DotNet.Highcharts.Options.Point { Color = GetBarColour(y), Y = y }).ToArray()
            //);

            chart.SetSeries(new[]
            {
                //new Series { Type = ChartTypes.Column, Name = "Production", Data = new Data(production) },
                //new Series { Type = ChartTypes.Column, Name = "Def.", Data = new Data(percent) },
                new Series { Type = ChartTypes.Spline, Color = Color.Red, Name = "Defective", Data = new Data(defective) }
            });

            return PartialView(chart);
        }

        [Chk_Authen]
        public ActionResult ByDefName()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        public ActionResult _GetGraphByDefType(string dtFrom, string dtTo, string fg)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 500
            })
            .SetTitle(new Title { Text = "Accumulate Defective by Defective Name" })
            .SetYAxis(new[]
            {
                new YAxis
                {
                    Labels = new YAxisLabels
                    {
                        Formatter = "function() { return this.value + '%'; }",
                        Style = "color: '#AA4643'"
                    },
                    Title = new YAxisTitle
                    {
                        Text = "Percent Def.",
                        Style = "color: '#AA4643'"
                    },
                    Min = 0
                },
                new YAxis
                {
                    Labels = new YAxisLabels
                    {
                        Formatter = "function() { return this.value + ' pcs.'; }",
                        Style = "color: '#89A54E'"
                    },
                    Title = new YAxisTitle
                    {
                        Text = "Def.",
                        Style = "color: '#89A54E'"
                    },
                    Opposite = true,
                    Min = 0
                }
            })
            //.SetYAxis(new YAxis
            //{
            //    Min = 0,//Start at
            //    Title = new YAxisTitle { Text = "Percent Defective" }
            //})
            //.SetTooltip(new Tooltip { PointFormat = "<b>{point.y:,.2f}</b> %" })
            .SetTooltip(new Tooltip
            {
                Enabled = false
                //PointFormat = "<b>{point.y:,.2f}</b>%" 
            })
            .SetPlotOptions(new PlotOptions
            {
                Column = new PlotOptionsColumn
                {
                    DataLabels = new PlotOptionsColumnDataLabels
                    {
                        Enabled = true,
                        Formatter = "function() { return Highcharts.numberFormat(this.y, 2) + '%'; }"
                    }
                },
                Line = new PlotOptionsLine
                {
                    DataLabels = new PlotOptionsLineDataLabels
                    {
                        Enabled = true,
                        Formatter = "function() { return this.y + ' pcs.'; }"
                    }
                },
                Pie = new PlotOptionsPie
                {
                    Center = new[] { new PercentageOrPixel(900), new PercentageOrPixel(80) },
                    Size = new PercentageOrPixel(150),
                    ShowInLegend = false,
                    //DataLabels = new PlotOptionsPieDataLabels { Enabled = true }
                    DataLabels = new PlotOptionsPieDataLabels
                    {
                        //Color = ColorTranslator.FromHtml("#000000"),
                        //ConnectorColor = ColorTranslator.FromHtml("#000000"),
                        //Enabled = true,
                        //Distance = -30,
                        Formatter = "function() { return '<b>'+ this.point.name +'</b>: '+ Highcharts.numberFormat(this.percentage, 2) +' %'; }"
                    }
                }
            })
            .SetLegend(new Legend
            {
                Enabled = true
                //Layout = Layouts.Vertical,
                //Align = HorizontalAligns.Right,
                //VerticalAlign = VerticalAligns.Top,
                //X = -50,
                //Y = 50,
                //Floating = true,
                //BorderWidth = 1,
                //BackgroundColor = new BackColorOrGradient(ColorTranslator.FromHtml("#FFFFFF")),
                //Shadow = true
            })
            .SetCredits(new Credits { Enabled = false });

            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            //var get_target = dbDef.tm_defective_prod_code.Where(w => w.product_code == fg).Select(s => s.defective_rate).FirstOrDefault();

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       //join b in dbDef.tm_defective_type on a.defective_type equals b.defective_id into ab
                       //from b in ab.DefaultIfEmpty()
                       select new { item = a.eng_code, job = a.job_order_no, def = a.qty, prd = a.curing_qty, tid = a.defective_type, tname = a.defective_name };

            var get_def = (from p in temp
                          group p by new { p.tid, p.tname } into g
                          select new { tid = g.Key.tid, tname = g.Key.tname, def = g.Sum(p => p.def) }).OrderByDescending(o => o.def);

            var get_prod = from a in temp
                           group a by a.job into g
                           select new{ id = g.Key, prod = g.Max(p => p.prd) };
            var prod = get_prod.Sum(s => s.prod);
            
            var get_type = get_def.Select(s => s.tname).ToArray();

            var count = get_def.Count();
            
            object[] percent = new object[count];
            object[] target = new object[count];
            //object[] production = new object[count];
            object[] def = new object[count];
            var defective = new List<object[]>();

            chart.SetXAxis(new XAxis
            {
                Categories = get_type,
                Title = new XAxisTitle { Text = "Defective Type" },
                Labels = new XAxisLabels
                {
                    Rotation = -45,
                    Align = HorizontalAligns.Right
                }
            });

            int i = 0;
            foreach (var item in get_def)
            {
                if (item.def != null)
                {
                    percent[i] = item.def * 100.0 / prod;
                    //production[i] = prod;
                    def[i] = item.def;
                }
                else
                {
                    percent[i] = 0;
                    //production[i] = 0;
                    def[i] = 0;
                }
                defective.Add(new object[] { item.tname, item.def });
                //target[i] = get_target;
                i++;
            }

            chart.SetSeries(new[]
            {
                new Series { Type = ChartTypes.Column, Name = "%Def.", Data = new Data(percent) },
                new Series { Type = ChartTypes.Line, Name = "Def.", Data = new Data(def), YAxis = "1" },
                new Series { Type = ChartTypes.Pie, Name = "Defective", Data = new Data(defective.ToArray()) }
                //new Series { Type = ChartTypes.Line, Name = "Target", Data = new Data(target) }
                //new Series { Type = ChartTypes.Column, Name = "Production", Data = new Data(production) },
            });

            ViewBag.TotalCuring = prod;

            return PartialView(chart);
        }

        [Chk_Authen]
        public ActionResult ByMonth()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        public ActionResult _GetGraphByMonth(string dtFrom, string dtTo, string fg)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 500
            })
            .SetTitle(new Title { Text = "Accumulate Defective by Month" })
            .SetYAxis(new YAxis
            {
                Min = 0,//Start at
                Title = new YAxisTitle { Text = "Percent Defective" }
            })
            .SetTooltip(new Tooltip { PointFormat = "<b>{point.y:,.2f}</b> %" })
                //.SetLegend(new Legend
                //{
                //    Layout = Layouts.Vertical,
                //    Align = HorizontalAligns.Right,
                //    VerticalAlign = VerticalAligns.Top,
                //    X = -50,
                //    Y = 50,
                //    Floating = true,
                //    BorderWidth = 1,
                //    BackgroundColor = new BackColorOrGradient(ColorTranslator.FromHtml("#FFFFFF")),
                //    Shadow = true
                //})
            .SetPlotOptions(new PlotOptions
            {
                Spline = new PlotOptionsSpline
                {
                    Marker = new PlotOptionsSplineMarker
                    {
                        Enabled = false
                    }
                },
                Column = new PlotOptionsColumn
                {
                    DataLabels = new PlotOptionsColumnDataLabels
                    {
                        Enabled = true,
                        Formatter = "function() { return Highcharts.numberFormat(this.y, 2) + '%'; }"
                    }
                }
            })
            .SetCredits(new Credits { Enabled = false });

            string[] tdf = dtFrom.Split('/');
            //string dfm = tdf[0] + tdf[1] + "01";
            string[] tdt = dtTo.Split('/');
            //string dto = tdt[0] + tdt[1] + "31";

            var get_target = dbDef.tm_defective_prod_code.Where(w => w.product_code == fg).Select(s => s.defective_rate).FirstOrDefault();

            byte mfm = byte.Parse(tdf[1]);
            byte mto = byte.Parse(tdt[1]);

            CultureInfo cultureinfo = new CultureInfo("en-US");

            if (tdf[0].CompareTo(tdt[0]) == 0 && mfm <= mto)//1 Year
            {
                var count = mto - mfm + 1;
                object[] percent = new object[count];
                object[] target = new object[count];
                //object[] production = new object[count];
                //object[] defective = new object[count];
                string[] mm = new string[count];
                int j = 0;
                for (byte i = mfm; i <= mto; i++)
                {
                    var ym = tdf[0] + i.ToString("00");

                    var temp = from a in dbDef.td_defective_data
                               where a.start_curing_date.CompareTo(ym + "01") >= 0 && a.start_curing_date.CompareTo(ym + "31") <= 0 && a.prod_code == fg
                               group a by new { a.job_order_no, a.curing_qty } into g
                               select new { job = g.Key.job_order_no, prod = g.Key.curing_qty, def = g.Sum(s => s.qty) };

                    mm[j] = cultureinfo.DateTimeFormat.GetMonthName(i);
                    percent[j] = 100.0 * temp.Sum(s => s.def) / temp.Sum(s => s.prod);
                    target[j] = get_target;
                    //production[j] = temp.Sum(s => s.prod);
                    //defective[j] = temp.Sum(s => s.def);

                    j++;
                }

                chart.SetXAxis(new XAxis
                {
                    Categories = mm,
                    Title = new XAxisTitle { Text = "Month" },
                    Labels = new XAxisLabels
                    {
                        Rotation = -45,
                        Align = HorizontalAligns.Right
                    }
                });
                chart.SetSeries(new[]
                {
                    new Series { Type = ChartTypes.Column, Name = "Def.", Data = new Data(percent) },
                    new Series { Type = ChartTypes.Spline, Color = Color.Red, Name = "Target", Data = new Data(target) }
                    //new Series { Type = ChartTypes.Column, Name = "Production", Data = new Data(production) },
                    //new Series { Type = ChartTypes.Column, Name = "Def.", Data = new Data(defective) }
                });
            }
            else if (tdf[0].CompareTo(tdt[0]) == -1)//Year from < Year to
            {
                var count = 12 - mfm + mto + 1;
                object[] percent = new object[count];
                object[] target = new object[count];
                string[] mm = new string[count];

                int j = 0;
                for (byte i = mfm; i <= 12; i++)
                {
                    var ym = tdf[0] + i.ToString("00");

                    var temp = from a in dbDef.td_defective_data
                               where a.start_curing_date.CompareTo(ym + "01") >= 0 && a.start_curing_date.CompareTo(ym + "31") <= 0 && a.prod_code == fg
                               group a by new { a.job_order_no, a.curing_qty } into g
                               select new { job = g.Key.job_order_no, prod = g.Key.curing_qty, def = g.Sum(s => s.qty) };

                    mm[j] = cultureinfo.DateTimeFormat.GetMonthName(i);
                    percent[j] = 100.0 * temp.Sum(s => s.def) / temp.Sum(s => s.prod);
                    target[j] = get_target;
                    //production[j] = temp.Sum(s => s.prod);
                    //defective[j] = temp.Sum(s => s.def);

                    j++;
                }

                for (int i = 1; i <= mto; i++)
                {
                    var ym = tdt[0] + i.ToString("00");
                    var temp = from a in dbDef.td_defective_data
                               where a.start_curing_date.CompareTo(ym + "01") >= 0 && a.start_curing_date.CompareTo(ym + "31") <= 0 && a.prod_code == fg
                               group a by new { a.job_order_no, a.curing_qty } into g
                               select new { job = g.Key.job_order_no, prod = g.Key.curing_qty, def = g.Sum(s => s.qty) };

                    mm[j] = cultureinfo.DateTimeFormat.GetMonthName(i);
                    percent[j] = 100.0 * temp.Sum(s => s.def) / temp.Sum(s => s.prod);
                    target[j] = get_target;

                    j++;
                }

                chart.SetXAxis(new XAxis
                {
                    Categories = mm,
                    Title = new XAxisTitle { Text = "Month" },
                    Labels = new XAxisLabels
                    {
                        Rotation = -45,
                        Align = HorizontalAligns.Right
                    }
                });
                chart.SetSeries(new[]
                {
                    new Series { Type = ChartTypes.Column, Name = "Def.", Data = new Data(percent) },
                    new Series { Type = ChartTypes.Spline, Color = Color.Red, Name = "Target", Data = new Data(target) }
                });
            }

            return PartialView(chart);
        }

        [Chk_Authen]
        public ActionResult PercentDef()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        public ActionResult _GetGraphPercentDef(string dtFrom, string dtTo, string fg)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 500,
                PlotShadow = false
            })
            .SetTitle(new Title { Text = "Defective" })
            .SetTooltip(new Tooltip { Formatter = "function() { return '<b>'+ this.point.name +'</b>: '+ Highcharts.numberFormat(this.percentage, 2) +' %'; }" })
            .SetPlotOptions(new PlotOptions
            {
                Pie = new PlotOptionsPie
                {
                    AllowPointSelect = true,
                    Cursor = Cursors.Pointer,
                    ShowInLegend = true,
                    DataLabels = new PlotOptionsPieDataLabels
                    {
                        Color = ColorTranslator.FromHtml("#000000"),
                        ConnectorColor = ColorTranslator.FromHtml("#000000"),
                        Formatter = "function() { return '<b>'+ this.point.name +'</b>: '+ Highcharts.numberFormat(this.percentage, 2) +' %'; }"
                    }
                }
            })
            .SetCredits(new Credits { Enabled = false });

            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];
            
            var get_def = (from a in dbDef.td_defective_data
                          where a.prod_code == fg &&
                          a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                          group a by a.defective_name into g
                          select new { name = g.Key, def = g.Sum(p => p.qty) }).OrderBy(o => o.def);
            
            var defective = new List<object[]>();

            foreach (var item in get_def)
            {
                defective.Add(new object[] { item.name, item.def });
            }

            chart.SetSeries(new[]
            {
                new Series { Type = ChartTypes.Pie, Name = "Defective", Data = new Data(defective.ToArray()) }
            });

            return PartialView(chart);
        }
        
        public ActionResult ItemDef()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        private Color GetBarColour(float value)
        {
            if (value < 5) return Color.Red;
            if (value > 7) return Color.Green;
            return Color.Orange;
        }

        public ActionResult _GetGraphItemDef(string dtFrom, string dtTo, string fg)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 500
            })
            .SetTitle(new Title { Text = "Accumulate Defective by Item" })
            .SetYAxis(new[]
            {
                new YAxis
                {
                    Labels = new YAxisLabels
                    {
                        Formatter = "function() { return this.value + ' pcs.'; }",
                        Style = "color: '#4572A7'"
                    },
                    Title = new YAxisTitle
                    {
                        Text = "Production",
                        Style = "color: '#4572A7'"
                    }
                },
                new YAxis
                {
                    Labels = new YAxisLabels
                    {
                        Formatter = "function() { return this.value + ' pcs.'; }",
                        Style = "color: '#AA4643'"
                    },
                    Title = new YAxisTitle
                    {
                        Text = "Defective",
                        Style = "color: '#AA4643'"
                    }
                },
                new YAxis
                {
                    Labels = new YAxisLabels
                    {
                        Formatter = "function() { return this.value + '%'; }",
                        Style = "color: '#89A54E'"
                    },
                    Title = new YAxisTitle
                    {
                        Text = "Percent",
                        Style = "color: '#89A54E'"
                    },
                    Opposite = true
                }
                    
            })
            .SetPlotOptions(new PlotOptions
            {
                Column = new PlotOptionsColumn
                {
                    DataLabels = new PlotOptionsColumnDataLabels
                    {
                        Enabled = true,
                        Rotation = -90,
                        X = 4,
                        Y = -20
                    }
                },
                Spline = new PlotOptionsSpline
                {
                    DataLabels = new PlotOptionsSplineDataLabels
                    {
                        Enabled = true,
                        Formatter = "function() { return Highcharts.numberFormat(this.y, 2) + '%'; }" 
                    }
                }
            })
            .SetTooltip(new Tooltip
            {
                Enabled = false
                //PointFormat = "<b>{point.y:,.2f}</b>%" 
            })
            .SetCredits(new Credits { Enabled = false });

            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            var get_target = dbDef.tm_defective_prod_code.Where(w => w.product_code == fg).Select(s => s.defective_rate).FirstOrDefault();

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       select new { item = a.eng_code, job = a.job_order_no, def = a.qty, prd = a.curing_qty };

            var get_def = (from p in temp
                          group p by p.item into g
                          select new { item = g.Key, def = g.Sum(p => p.def) }).OrderByDescending(o => o.def);

            var get_item = get_def.Select(s => s.item).ToArray();

            var count = get_def.Count();

            object[] production = new object[count];
            object[] defective = new object[count];
            object[] percent = new object[count];

            chart.SetXAxis(new XAxis
            {
                Categories = get_item,
                Title = new XAxisTitle { Text = "Item" },
                Labels = new XAxisLabels
                {
                    Rotation = -45,
                    Align = HorizontalAligns.Right
                }
            });

            int i = 0;
            foreach (var item in get_def)
            {
                if (item.def != null)
                {
                    var temp1 = temp.Where(w => w.item == item.item).Select(s => new { s.job, s.prd }).Distinct().Sum(s => s.prd);
                    production[i] = temp1;
                    defective[i] = item.def;
                    percent[i] = item.def * 100.0 / temp1;
                }
                else
                {
                    production[i] = 0;
                    defective[i] = 0;
                    percent[i] = 0;
                }
                i++;
            }

            chart.SetSeries(new[]
            {
                new Series { 
                    Type = ChartTypes.Column,
                    Name = "Defective",
                    Color = ColorTranslator.FromHtml("#AA4643"),
                    YAxis = "1",
                    Data = new Data(defective)
                },
                new Series { 
                    Type = ChartTypes.Column,
                    Name = "Production",
                    Color = ColorTranslator.FromHtml("#4572A7"),
                    
                    Data = new Data(production)
                },
                new Series { 
                    Type = ChartTypes.Spline,
                    Name = "Percent",
                    Color = ColorTranslator.FromHtml("#89A54E"),
                    YAxis = "2",
                    Data = new Data(percent)
                }
            });

            return PartialView(chart);
        }

        public ActionResult ItemTable()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        public ActionResult _GetTableByItem(string dtFrom, string dtTo, string fg)
        {
            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            //var get_def_type = 

            return PartialView();
        }

        [Chk_Authen]
        public ActionResult ByItemDetail()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        public ActionResult DateTable()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        [Chk_Authen]
        public ActionResult DefModeTo3D()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        public ActionResult _TableDefMode(string dtFrom, string dtTo, string fg, string eng_code = "")
        {
            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       join b in dbDef.tm_defective_type on a.defective_type equals b.defective_id into ab
                       from b in ab.DefaultIfEmpty()
                       select new { item = a.eng_code, job = a.job_order_no, def = a.qty, prd = a.curing_qty, 
                                    tid = b.defective_id, tname = b.defective_name, curdt = a.start_curing_date };

            //var query = dbDef.td_defective_data.Where(w => w.prod_code == fg 
            //            && w.start_curing_date.CompareTo(dfm) >= 0 && w.start_curing_date.CompareTo(dto) <= 0);

            if (!string.IsNullOrEmpty(eng_code))
            {
                temp = temp.Where(w => w.item == eng_code);
            }

            var query1 = from p in temp
                         group p by p.tname into g
                         select new { def_type = g.Key, def = g.Sum(s => s.def) };

            ViewBag.DefMode = query1.OrderBy(o => o.def);

            return PartialView();
        }

        public JsonResult TableDefMode(string dtFrom, string dtTo, string fg, string item = "")
        {
            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       //join b in dbDef.tm_defective_type on a.defective_type equals b.defective_id into ab
                       //from b in ab.DefaultIfEmpty()
                       select new
                       {
                           item = a.eng_code,
                           job = a.job_order_no,
                           def = a.qty,
                           prd = a.curing_qty,
                           tid = a.defective_type,
                           tname = a.defective_name,
                           curdt = a.start_curing_date
                       };

            //var query = dbDef.td_defective_data.Where(w => w.prod_code == fg 
            //            && w.start_curing_date.CompareTo(dfm) >= 0 && w.start_curing_date.CompareTo(dto) <= 0);

            if (!string.IsNullOrEmpty(item))
            {
                temp = temp.Where(w => w.item == item);
            }

            var query1 = (from p in temp
                          group p by new { p.tid, p.tname } into g
                          select new { tid = g.Key.tid, tname = g.Key.tname, def = g.Sum(p => p.def) }).OrderByDescending(o => o.def);

            return Json(query1, JsonRequestBehavior.AllowGet);
        }

        public JsonResult TableItem(string dtFrom, string dtTo, string fg, int mode = 0)
        {
            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       //join b in dbDef.tm_defective_type on new { a.defective_type, a.location_cd } equals new { b.defective_id, b.location_cd } into ab
                       //from b in ab.DefaultIfEmpty()
                       select new
                       {
                           item = a.eng_code,
                           //job = a.job_order_no,
                           def = a.qty,
                           prd = a.curing_qty,
                           tid = a.defective_type,
                           //tname = b.defective_name,
                           curdt = a.start_curing_date
                       };

            if (mode != 0)
            {
                temp = temp.Where(w => w.tid == mode);
            }

            var query1 = (from p in temp
                          group p by p.item into g
                          select new { item = g.Key, def = g.Sum(p => p.def) }).OrderByDescending(o => o.def);

            return Json(query1, JsonRequestBehavior.AllowGet);
        }

        public JsonResult TableWC(string dtFrom, string dtTo, string fg, string item = "", int mode = 0)
        {
            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       //join b in dbDef.tm_defective_type on a.defective_type equals b.defective_id into ab
                       //from b in ab.DefaultIfEmpty()
                       select new
                       {
                           item = a.eng_code,
                           //job = a.job_order_no,
                           def = a.qty,
                           prd = a.curing_qty,
                           tid = a.defective_type,
                           //tname = b.defective_name,
                           wc = a.wc + "/" + a.machine_no,
                           curdt = a.start_curing_date
                       };

            if (!string.IsNullOrEmpty(item))
            {
                temp = temp.Where(w => w.item == item);
            }
            if (mode != 0)
            {
                temp = temp.Where(w => w.tid == mode);
            }

            var query1 = (from p in temp
                          group p by p.wc into g
                          select new { wc = g.Key, def = g.Sum(p => p.def) }).OrderByDescending(o => o.def);

            return Json(query1, JsonRequestBehavior.AllowGet);
        }

        public JsonResult TableDay(string dtFrom, string dtTo, string fg, string item = "", int mode = 0)
        {
            string[] tdf = dtFrom.Split('/');
            string dfm = tdf[2] + tdf[1] + tdf[0];
            string[] tdt = dtTo.Split('/');
            string dto = tdt[2] + tdt[1] + tdt[0];

            var temp = from a in dbDef.td_defective_data
                       where a.prod_code == fg &&
                       a.start_curing_date.CompareTo(dfm) >= 0 && a.start_curing_date.CompareTo(dto) <= 0
                       //join b in dbDef.tm_defective_type on a.defective_type equals b.defective_id into ab
                       //from b in ab.DefaultIfEmpty()
                       select new
                       {
                           item = a.eng_code,
                           //job = a.job_order_no,
                           def = a.qty,
                           prd = a.curing_qty,
                           tid = a.defective_type,
                           //tname = b.defective_name,
                           //wc = a.wc + "/" + a.machine_no,
                           curdt = a.start_curing_date
                       };

            if (!string.IsNullOrEmpty(item))
            {
                temp = temp.Where(w => w.item == item);
            }
            if (mode != 0)
            {
                temp = temp.Where(w => w.tid == mode);
            }

            var query1 = (from p in temp
                          group p by p.curdt into g
                          select new { dt = g.Key, def = g.Sum(p => p.def) }).OrderBy(o => o.dt);//.OrderByDescending(o => o.def);

            return Json(query1, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ByDaily()
        {
            ViewBag.SelectFG = (from a in dbQim.tm_master_item
                                where a.product_code != ""
                                select a.product_code).Distinct().OrderBy(o => o);
            return View();
        }

        public ActionResult _GetGraphByDaily(string fg, string yymm)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 500
            })
            .SetTitle(new Title { Text = "Daily Defective" })
            .SetYAxis(new YAxis
            {
                Min = 0,//Start at
                Title = new YAxisTitle { Text = "Percent Defective" }
            })
            .SetTooltip(new Tooltip { PointFormat = "<b>{point.y:,.2f}</b> %" })
            .SetPlotOptions(new PlotOptions
            {
                Spline = new PlotOptionsSpline
                {
                    Marker = new PlotOptionsSplineMarker
                    {
                        Enabled = false
                    }
                },
                Column = new PlotOptionsColumn
                {
                    DataLabels = new PlotOptionsColumnDataLabels
                    {
                        Enabled = true,
                        Formatter = "function() { return Highcharts.numberFormat(this.y, 2) + '%'; }"
                    }
                }
            })
            .SetCredits(new Credits { Enabled = false });

            string[] tdf = yymm.Split('/');
            int days = DateTime.DaysInMonth(int.Parse(tdf[0]), int.Parse(tdf[1]));
            string[] x_axis = new string[days];

            var get_target = dbDef.tm_defective_prod_code.Where(w => w.product_code == fg).Select(s => s.defective_rate).FirstOrDefault();

            CultureInfo cultureinfo = new CultureInfo("en-US");

            object[] percent = new object[days];
            object[] target = new object[days];
            int[] production = new int[days];
            int[] defective = new int[days];
            
            for (byte i = 0; i < days; i++)
            {
                x_axis[i] = (i + 1).ToString("00");
                var ymd = tdf[0] + tdf[1] + x_axis[i];

                var temp = from a in dbDef.td_defective_data
                           where a.start_curing_date == ymd && a.prod_code == fg
                           group a by new { a.job_order_no, a.curing_qty } into g
                           select new { job = g.Key.job_order_no, prod = g.Key.curing_qty, def = g.Sum(s => s.qty) };

                if (temp.Any())
                {
                    production[i] = temp.Sum(s => s.prod).Value;
                    defective[i] = temp.Sum(s => s.def).Value;
                    percent[i] = 100.0 * defective[i] / production[i];
                    target[i] = get_target;
                }
                else
                {
                    production[i] = 0;
                    defective[i] = 0;
                    percent[i] = 0;
                    target[i] = get_target;
                }
            }

            //ViewBag.Count = days;
            //ViewBag.DataProd = production;
            //ViewBag.DataDef = defective;
            //ViewBag.DataPerc = Array.ConvertAll<object, double>(percent, o => (double)o);

            chart.SetXAxis(new XAxis
            {
                Categories = x_axis,
                Title = new XAxisTitle { Text = "Day" }
                //Labels = new XAxisLabels
                //{
                //    Rotation = -45,
                //    Align = HorizontalAligns.Right
                //}
            });
            chart.SetSeries(new[]
            {
                new Series { Type = ChartTypes.Column, Name = "%Def.", Data = new Data(percent) },
                new Series { Type = ChartTypes.Spline, Color = Color.Red, Name = "Target", Data = new Data(target) }
                //new Series { Type = ChartTypes.Column, Name = "Production", Data = new Data(production) },
                //new Series { Type = ChartTypes.Column, Name = "Def.", Data = new Data(defective) }
            });

            return PartialView(chart);
        }

        public ActionResult BI()
        {
            return View();
        }

        public ActionResult BI1()
        {
            return View();
        }
    }
}
