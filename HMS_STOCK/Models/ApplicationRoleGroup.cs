using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMS_STOCK.Models
{
    public class ApplicationRoleGroup
    {
        public virtual string RoleId { get; set; }
        public virtual int GroupId { get; set; }

        public virtual ApplicationRole Role { get; set; }
        public virtual Group Group { get; set; }
    }
}