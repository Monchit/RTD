﻿//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace RealTimeDefective.Models
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class defectiveliveEntities : DbContext
    {
        public defectiveliveEntities()
            : base("name=defectiveliveEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public DbSet<tm_defective_cure_date> tm_defective_cure_date { get; set; }
        public DbSet<tm_defective_item> tm_defective_item { get; set; }
        public DbSet<tm_defective_prod_code> tm_defective_prod_code { get; set; }
        public DbSet<tm_defective_rate_job> tm_defective_rate_job { get; set; }
        public DbSet<tm_defective_rate_type> tm_defective_rate_type { get; set; }
        public DbSet<tm_defective_type> tm_defective_type { get; set; }
        public DbSet<tm_defective_wc> tm_defective_wc { get; set; }
        public DbSet<td_defective_data> td_defective_data { get; set; }
    }
}
