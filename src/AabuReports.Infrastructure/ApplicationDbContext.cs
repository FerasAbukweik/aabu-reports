using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AabuReports.Infrastructure;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // refresh token relations
        builder.Entity<RefreshToken>(rt =>
        {
            rt 
                .HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            rt.HasIndex(rt => rt.RefreshTokenString)
                .IsUnique();
        });

        // one account per student
        builder.Entity<ApplicationUser>(u =>
        {
            u
                .HasIndex(u => u.StudentId)
                .IsUnique();
        });

        // location: coordinates are stored as columns of the Locations table
        builder.Entity<Location>(location =>
        {
            location.OwnsOne(l => l.Coordinates, coordinates =>
            {
                coordinates.Property(c => c.Lat).HasColumnName("Lat").IsRequired();
                coordinates.Property(c => c.Lng).HasColumnName("Lng").IsRequired();
                
                coordinates.HasIndex(x => new { x.Lat, x.Lng }).IsUnique();
            });
            
            location.Ignore(l => l.State);
            location.Ignore(l => l.ReportsCount);
        });

        // report
        builder.Entity<Report>(report =>
        {
            // one location has many reports, deleting the location deletes its reports
            report.HasOne(r => r.Location)
                .WithMany(l => l.Reports)
                .HasForeignKey(r => r.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            // the user who sent it: users with reports cannot be deleted
            report.HasOne(r => r.User)
                .WithMany(u => u.Reports)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // image: a report has many images, an image belongs to one report,
        // deleting a report sets ReportId to null (the images are removed later by RemoveOrphanedImages)
        builder.Entity<Image>(image =>
        {
            image.HasOne(i => i.Report)
                .WithMany(r => r.Images)
                .HasForeignKey(i => i.ReportId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            image.HasIndex(i => i.PublicId)
                .IsUnique();
        });

        // notification: deleting the user deletes their notifications,
        // deleting a report keeps the notification (ReportId becomes null, the Subject is a copy so it still makes sense)
        builder.Entity<Notification>(notification =>
        {
            notification.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            notification.HasOne(n => n.Report)
                .WithMany()
                .HasForeignKey(n => n.ReportId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // the list, the unread filter and the unread count are all "by user, newest first"
            notification.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        });

        // message: deleting a report deletes its chat,
        // users who wrote messages cannot be deleted (same as users with reports)
        builder.Entity<Message>(message =>
        {
            message.HasOne(m => m.Report)
                .WithMany(r => r.Messages)
                .HasForeignKey(m => m.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            message.HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // the chat is always loaded "by report, newest first"
            message.HasIndex(m => new { m.ReportId, m.SentAt });
        });

        // store every DateTime as UTC and read it back as UTC
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTime, DateTime>(
                            v => v.ToUniversalTime(),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
            }
        }
    }
    
    
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<Report> Reports { get; set; }
    public virtual DbSet<Image> Images { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
}
