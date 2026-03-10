using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System;

namespace HMS_STOCK.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
            : base()
        {
            this.Groups = new HashSet<ApplicationUserGroup>();
        }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string Email { get; set; }

        public string NPassword { get; set; }

        // Stores the relative/absolute path of the uploaded Government Proof file
        [Display(Name = "Government Proof Path")]
        [NotMapped]
        public string GovernmentProofPath { get; set; }

        public virtual ICollection<ApplicationUserGroup> Groups { get; set; }
    }
}