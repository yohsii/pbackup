﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using puck.core.Abstract;
using puck.core.Entities;
using puck.core.State;

namespace puck.core.Concrete
{
    public partial class PuckContext : IdentityDbContext<PuckUser,PuckRole,string
        ,IdentityUserClaim<string>,PuckUserRole,IdentityUserLogin<string>
        ,IdentityRoleClaim<string>,IdentityUserToken<string>> , I_Puck_Context
    {
        public static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        
        private string connectionString;
        public PuckContext(DbContextOptions options):base(options)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
        }
        public PuckContext(string connectionString)
        {
            this.connectionString = connectionString;
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            if(PuckCache.Debug)
                optionsBuilder.UseLoggerFactory(_loggerFactory);
            if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(connectionString)) {
                optionsBuilder.UseSqlServer(connectionString);
            }
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<PuckRevision>(e=>e.Ignore(x=>x.References));
            builder.Entity<PuckMeta>(e=> {
                e.HasIndex(x=>x.Name);
                //e.HasIndex(x=>x.Key);
            });
            builder.Entity<PuckRevision>(e => {
                e.HasIndex(x=>x.Id);
                e.HasIndex(x => x.ParentId);
                e.HasIndex(x => x.Current);
                e.HasIndex(x => x.Variant);
                e.HasIndex(x => x.HasNoPublishedRevision);
                e.HasIndex(x => x.IsPublishedRevision);
                e.HasIndex(x => x.Type);
            });
            builder.Entity<PuckInstruction>(e => {
                e.HasIndex(x=>x.ServerName);
            });
            builder.Entity<PuckAudit>(e => {
                e.HasIndex(x=>x.Action);
                e.HasIndex(x => x.ContentId);
                e.HasIndex(x => x.Timestamp);
                e.HasIndex(x => x.Variant);
                e.HasIndex(x => x.UserName);
            });
            builder.Entity<PuckWorkflowItem>(e => {
                e.HasIndex(x => x.Complete);
                e.HasIndex(x => x.CompleteDate);
                e.HasIndex(x => x.ContentId);
                e.HasIndex(x => x.Variant);
                e.HasIndex(x => x.Group);
                e.HasIndex(x => x.LockedBy);
                e.HasIndex(x => x.LockedUntil);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.Timestamp);
                e.HasIndex(x => x.AddedBy);
            });

            builder.Entity<PuckUser>(b =>
            {
                // Each User can have many UserClaims
                b.HasMany(e => e.Claims)
                    .WithOne()
                    .HasForeignKey(uc => uc.UserId)
                    .IsRequired();

                // Each User can have many UserLogins
                b.HasMany(e => e.Logins)
                    .WithOne()
                    .HasForeignKey(ul => ul.UserId)
                    .IsRequired();

                //// Each User can have many UserTokens
                //b.HasMany(e => e.Tokens)
                //    .WithOne()
                //    .HasForeignKey(ut => ut.UserId)
                //    .IsRequired();

                // Each User can have many entries in the UserRole join table
                b.HasMany(e => e.Roles)
                    .WithOne(e => e.User)
                    .HasForeignKey(ur => ur.UserId)
                    .IsRequired();
            });

            builder.Entity<PuckRole>(b =>
            {
                // Each Role can have many entries in the UserRole join table
                b.HasMany(e => e.UserRoles)
                    .WithOne(e => e.Role)
                    .HasForeignKey(ur => ur.RoleId)
                    .IsRequired();
            });
        }
        public DbSet<PuckMeta> PuckMeta { get; set; }
        public DbSet<PuckRevision> PuckRevision { get; set; }
        public DbSet<PuckInstruction> PuckInstruction { get; set; }
        public DbSet<PuckAudit> PuckAudit { get; set; }
        public DbSet<PuckTag> PuckTag { get; set; }
        public DbSet<PuckRedirect> PuckRedirect { get; set; }
        public DbSet<PuckWorkflowItem> PuckWorkflowItem { get; set; }
        //public DbSet<GeneratedModel> GeneratedModel { get; set; }
        //public DbSet<GeneratedProperty> GeneratedProperty { get; set; }
        //public DbSet<GeneratedAttribute> GeneratedAttribute { get; set; }

    }
}
