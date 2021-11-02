using System.Collections.Generic;

namespace Okr_Lambda
{
    public class Payload<T>
    {
        /// <summary>
        /// Gets or sets the view model.
        /// </summary>
        /// <value>
        /// The view model.
        /// </value>
        public T Entity { get; set; }

        /// <summary>
        /// Gets or sets the view model list.
        /// </summary>
        /// <value>
        /// The view model list.
        /// </value>
        public List<T> EntityList { get; set; }

        /// <summary>
        /// Gets or sets the message list.
        /// </summary>
        /// <value>
        /// The message list.
        /// </value>
        public List<string> MessageList { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        /// <value>
        /// The type of the message.
        /// </value>
        public string MessageType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is success.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is success; otherwise, <c>false</c>.
        /// </value>
        public bool IsSuccess { get; set; } = false;
    }
    public class PayloadCustom<T>
    {
        /// <summary>
        /// Gets or sets the view model.
        /// </summary>
        /// <value>
        /// The view model.
        /// </value>
        public T Entity { get; set; }

        /// <summary>
        /// Gets or sets the view model list.
        /// </summary>
        /// <value>
        /// The view model list.
        /// </value>
        public List<T> EntityList { get; set; }

        /// <summary>
        /// Gets or sets the message list.
        /// </summary>
        /// <value>
        /// The message list.
        /// </value>
       /// public List<string> MessageList { get; set; } = new List<string>();
        public Dictionary<string, string> MessageList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        /// <value>
        /// The type of the message.
        /// </value>
        public string MessageType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is success.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is success; otherwise, <c>false</c>.
        /// </value>
        public bool IsSuccess { get; set; } = false;

        public int Status { get; set; }
    }

    public class PayloadCustomList<T>
    {
        public T Entity { get; set; }
        /// <summary>
        /// Gets or sets the view model list.
        /// </summary>
        /// <value>
        /// The view model list.
        /// </value>
        public List<T> EntityList { get; set; }
        /// <summary>
        /// Gets or sets the message list.
        /// </summary>
        /// <value>
        /// The message list.
        /// </value>
        public Dictionary<string, string> MessageList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        /// <value>
        /// The type of the message.
        /// </value>
        public string MessageType { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is success.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is success; otherwise, <c>false</c>.
        /// </value>
        public bool IsSuccess { get; set; } = false;

        public int Status { get; set; }

        public PageResults<T> PaggingInfo { get; set; }
    }

    public class PageResults<T>
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int HeaderCode { get; set; }
        public List<T> Records { get; set; }
        public IEnumerable<T> Results { get; set; }
    }

    public class PayloadCustomPassport<T>
    {
        /// <summary>
        /// Gets or sets the view model.
        /// </summary>
        /// <value>
        /// The view model.
        /// </value>
        public T Entity { get; set; }

        /// <summary>
        /// Gets or sets the view model list.
        /// </summary>
        /// <value>
        /// The view model list.
        /// </value>
        public List<T> EntityList { get; set; }

        /// <summary>
        /// Gets or sets the message list.
        /// </summary>
        /// <value>
        /// The message list.
        /// </value>
        public List<string> MessageList { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        /// <value>
        /// The type of the message.
        /// </value>
        public string MessageType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is success.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is success; otherwise, <c>false</c>.
        /// </value>
        public bool IsSuccess { get; set; } = false;
    }
}

