using System;
using System.Collections.Generic;
using System.Text;

namespace Okr_Lambda.Models
{
    public class RoleMaster
    {
        public virtual long RoleId
        {
            get;
            set;
        }

        public virtual string RoleName
        {
            get;
            set;
        }

        public virtual string RoleDescription
        {
            get;
            set;
        }

        public virtual bool IsActive
        {
            get;
            set;
        }

        public virtual long CreatedBy
        {
            get;
            set;
        }

        public virtual System.DateTime CreatedOn
        {
            get;
            set;
        }

        public virtual long? UpdatedBy
        {
            get;
            set;
        }

        public virtual System.DateTime? UpdatedOn
        {
            get;
            set;
        }
    }
}
