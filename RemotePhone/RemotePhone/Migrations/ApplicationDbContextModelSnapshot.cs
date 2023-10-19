﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RemotePhone.Database;

#nullable disable

namespace RemotePhone.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.10");

            modelBuilder.Entity("RemotePhone.Models.RealPhone", b =>
                {
                    b.Property<string>("Serial")
                        .HasColumnType("TEXT");

                    b.Property<bool>("InUse")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("Lastvisit")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Serial");

                    b.ToTable("RealPhones");
                });

            modelBuilder.Entity("RemotePhone.Models.VirtualPhone", b =>
                {
                    b.Property<int>("Port")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("InUse")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("Lastvisit")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Port");

                    b.ToTable("VirtualPhones");
                });
#pragma warning restore 612, 618
        }
    }
}
