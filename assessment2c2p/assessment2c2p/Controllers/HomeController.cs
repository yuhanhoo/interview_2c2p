using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using assessment2c2p.Models;
using Microsoft.VisualBasic.FileIO;
using MySqlConnector;
using System.Data;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

namespace assessment2c2p.Controllers;

public class HomeController : Controller
{
    private readonly IConfiguration conf;

    public HomeController(IConfiguration conf)
    {
        this.conf = conf;
    }

    public IActionResult Index()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost("UploadFile")]
    public IActionResult UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Please select a file.");
        }

        string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string error = ValidInputFile(file, fileExtension);
        if (error.Length > 0) {
            return BadRequest(error);
        }

        if (fileExtension == ".csv")
            error = ExecuteCsvRecords(file);
        else if (fileExtension == ".xml")
            error = ExecuteXmlRecords(file);

        if (error.Length > 0)
            return BadRequest("File uploaded failed.\n" + error);
        else
            return Ok("File uploaded successfully.");
    }

    private string ExecuteCsvRecords(IFormFile file)
    {
        string error = "";

        try
        {
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                DataTable dtRecords = new DataTable();
                dtRecords.Columns.Add("id");
                dtRecords.Columns.Add("transaction_id");
                dtRecords.Columns.Add("amount");
                dtRecords.Columns.Add("currency_code");
                dtRecords.Columns.Add("transaction_date");
                dtRecords.Columns.Add("status");

                using (var csvParser = new TextFieldParser(reader))
                {
                    csvParser.SetDelimiters(new string[] { "," });

                    while (!csvParser.EndOfData)
                    {
                        string[] csvFields = csvParser.ReadFields();

                        if (csvFields.Length == 5)
                        {
                            for (int row = 0; row < csvFields.Length; row++)
                            {
                                if (string.IsNullOrEmpty(csvFields[row]))
                                {
                                    error = "Invalid CSV File.";
                                    break;
                                }

                                if (row == 3) {
                                    DateTime originalDateTime = DateTime.ParseExact(csvFields[3], "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                                    string formattedDateTimeString = originalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                                    csvFields[3] = formattedDateTimeString;
                                }

                                if (row == 4)
                                {
                                    csvFields[4] = GetStatusCode(csvFields[4]);
                                }
                            }

                            string[] newFields = new string[6];
                            newFields[0] = "";
                            Array.Copy(csvFields, 0, newFields, 1, csvFields.Length);
                            csvFields = newFields;

                            dtRecords.Rows.Add(csvFields);
                        }
                        else
                        {
                            error = "Invalid CSV File.";
                            break;
                        }
                    }
                }

                if (error.Length <= 0)
                {
                    InsertBulkRecords(dtRecords);
                }
            }
        }
        catch (Exception ex)
        {
            error = "Error while reading CSV file: " + ex.Message;
        }

        return error;
    }

    private string ExecuteXmlRecords(IFormFile file)
    {
        string error = "";

        try
        {
            XDocument doc = XDocument.Load(new StreamReader(file.OpenReadStream(), Encoding.UTF8));
            var Transactions = doc.Descendants("Transactions");

            DataTable dtRecords = new DataTable();
            dtRecords.Columns.Add("id");
            dtRecords.Columns.Add("transaction_id");
            dtRecords.Columns.Add("amount");
            dtRecords.Columns.Add("currency_code");
            dtRecords.Columns.Add("transaction_date");
            dtRecords.Columns.Add("status");

            var transactions = doc.Descendants("Transaction");
            foreach (var transaction in transactions)
            {
                var trans = new Transaction
                {
                    Id = transaction.Attribute("id")?.Value,
                    TransactionDate = DateTime.ParseExact(transaction.Element("TransactionDate")?.Value, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                    Amount = decimal.Parse(transaction.Element("PaymentDetails")?.Element("Amount")?.Value ?? "0"),
                    CurrencyCode = transaction.Element("PaymentDetails")?.Element("CurrencyCode")?.Value,
                    Status = GetStatusCode(transaction.Element("Status")?.Value)
                };

                if (!string.IsNullOrEmpty(trans.Id) &&
                    trans.TransactionDate != default &&
                    trans.Amount != 0 &&
                    !string.IsNullOrEmpty(trans.CurrencyCode) &&
                    !string.IsNullOrEmpty(trans.Status))
                {
                    dtRecords.Rows.Add("", trans.Id, trans.Amount,
                    trans.CurrencyCode, trans.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss"), trans.Status);
                }
                else {
                    error = "Invalid XML file.";
                    break;
                }
            }

            if (error.Length <= 0)
            {
                InsertBulkRecords(dtRecords);
            }
        }
        catch (Exception ex)
        {
            error = "Error while reading XML file: " + ex.Message;
        }

        return error;
    }

    private string GetStatusCode(string statusString)
    {
        string status = "";

        if (statusString.ToLower().Equals("approved"))
            status = "A";
        else if (statusString.ToLower().Equals("rejected") || statusString.ToLower().Equals("failed"))
            status = "R";
        else if (statusString.ToLower().Equals("finished") || statusString.ToLower().Equals("done"))
            status = "D";

        return status;
    }

    private string ValidInputFile(IFormFile file, string fileExtension)
    {
        string error = "";

        if (fileExtension != ".csv" && fileExtension != ".xml")
        {
            error = "Only CSV or XML files are allowed.";
        }

        if (file.Length > 1048576)
        {
            error = "File are not allow to exceeds 1MB.";
        }

        return error;
    }

    private void InsertBulkRecords(DataTable dataTable)
    {
        using (MySqlConnection connection = new MySqlConnection(conf.GetConnectionString("Default")))
        {
            MySqlBulkCopy mySqlBulkCopy = new MySqlBulkCopy(connection);
            mySqlBulkCopy.DestinationTableName = "transactions";
            connection.Open();
            mySqlBulkCopy.WriteToServer(dataTable);
            connection.Close();
        }
    }
}


