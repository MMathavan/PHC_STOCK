using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HMS_STOCK.Models
{
    /// <summary>
    /// DataTables server-side processing request parameters
    /// </summary>
    public class DataTableRequest
    {
        /// <summary>
        /// Draw counter for DataTables synchronization
        /// </summary>
        public int Draw { get; set; }

        /// <summary>
        /// Start row number for paging
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// Number of rows to display per page
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Global search value
        /// </summary>
        public DataTableSearch Search { get; set; }

        /// <summary>
        /// Column ordering information
        /// </summary>
        public List<DataTableOrder> Order { get; set; }

        /// <summary>
        /// Column data definitions
        /// </summary>
        public List<DataTableColumn> Columns { get; set; }

        /// <summary>
        /// Optional Material Group filter
        /// </summary>
        public int? MaterialGroupId { get; set; }
    }

    /// <summary>
    /// DataTables search parameter
    /// </summary>
    public class DataTableSearch
    {
        /// <summary>
        /// Search value
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Whether to treat as regex
        /// </summary>
        public bool Regex { get; set; }
    }

    /// <summary>
    /// DataTables column ordering parameter
    /// </summary>
    public class DataTableOrder
    {
        /// <summary>
        /// Column index to order by
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Direction: asc or desc
        /// </summary>
        public string Dir { get; set; }
    }

    /// <summary>
    /// DataTables column definition
    /// </summary>
    public class DataTableColumn
    {
        /// <summary>
        /// Column data property name
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Column header name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether column is searchable
        /// </summary>
        public bool Searchable { get; set; }

        /// <summary>
        /// Whether column is orderable
        /// </summary>
        public bool Orderable { get; set; }

        /// <summary>
        /// Column search value
        /// </summary>
        public DataTableSearch Search { get; set; }
    }

    /// <summary>
    /// DataTables server-side processing response
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    public class DataTableResponse<T>
    {
        /// <summary>
        /// Draw counter for DataTables synchronization
        /// </summary>
        [JsonProperty("draw")]
        public int Draw { get; set; }

        /// <summary>
        /// Total records without filtering
        /// </summary>
        [JsonProperty("recordsTotal")]
        public int RecordsTotal { get; set; }

        /// <summary>
        /// Total records after filtering
        /// </summary>
        [JsonProperty("recordsFiltered")]
        public int RecordsFiltered { get; set; }

        /// <summary>
        /// Data array to display
        /// </summary>
        [JsonProperty("data")]
        public List<T> Data { get; set; }

        /// <summary>
        /// Optional error message
        /// </summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }

    /// <summary>
    /// Simplified DataTables response for StockMaster_2526
    /// </summary>
    public class DataTableResponse : DataTableResponse<StockMaster_2526>
    {
    }
}
