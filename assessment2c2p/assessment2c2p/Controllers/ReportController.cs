
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace assessment2c2p.Controllers
{
    public class ReportController : Controller
    {
        private readonly IConfiguration conf;

        public ReportController(IConfiguration conf) {
            this.conf = conf;
        }

        [HttpGet]
        [Route("api/v1/GetTransaction")]
        public ActionResult<IEnumerable<Models.Transaction>> GetTransactionV1()
        {
            var transactionsReport = new List<Models.Report>();
            using (MySqlConnection connection = new MySqlConnection(conf.GetConnectionString("Default")))
            {
                connection.Open();

                HttpContext.Request.Query.TryGetValue("mode", out var searchBy);
                HttpContext.Request.Query.TryGetValue("value", out var searchValue);
                HttpContext.Request.Query.TryGetValue("startDate", out var searchStartDate);
                HttpContext.Request.Query.TryGetValue("endDate", out var searchEndDate);

                string query = "SELECT transaction_id, amount, currency_code, status FROM transactions ";

                if (searchBy.ToString().ToLower().Equals("currency"))
                    query += "where currency_code = @value";
                if (searchBy.ToString().ToLower().Equals("date"))
                    query += "where transaction_date between @startDate and @endDate";
                if (searchBy.ToString().ToLower().Equals("status"))
                    query += "where status = @value";

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.Add(new MySqlParameter("@value", searchValue.ToString()));

                    if (searchBy.ToString().ToLower().Equals("date"))
                    {
                        if (!string.IsNullOrEmpty(searchStartDate))
                            command.Parameters.Add(
                                new MySqlParameter("@startDate", DateTime.ParseExact(searchStartDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)));
                        if (!string.IsNullOrEmpty(searchEndDate))
                            command.Parameters.Add(
                                new MySqlParameter("@endDate", DateTime.ParseExact(searchEndDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)));
                    }

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var transaction = new Models.Report
                            {
                                Id = reader["transaction_id"].ToString() ?? "",
                                Payment = Convert.ToDecimal(reader["amount"]).ToString("F2") + " " + reader["currency_code"].ToString(),
                                Status = reader["Status"].ToString() ?? ""
                            };

                            transactionsReport.Add(transaction);
                        }
                    }
                }
            }

            return Ok(transactionsReport);
        }
    }
}

