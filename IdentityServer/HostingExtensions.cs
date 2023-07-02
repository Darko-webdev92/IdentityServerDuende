using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using IdentityModel;
using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Security.Claims;

namespace IdentityServer;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {


        var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        // uncomment if you want to add a UI
        builder.Services.AddRazorPages();

        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                         .AddEntityFrameworkStores<ApplicationDbContext>()
                         .AddDefaultTokenProviders();

        #region IN-Memmory
        //builder.Services.AddIdentityServer()
        //    .AddInMemoryIdentityResources(Config.IdentityResources)
        //    .AddInMemoryApiScopes(Config.ApiScopes)
        //    .AddInMemoryClients(Config.Clients)
        //    .AddTestUsers(TestUsers.Users);
        #endregion

        builder.Services.AddIdentityServer(options =>
                        {
                            options.Events.RaiseErrorEvents = true;
                            options.Events.RaiseInformationEvents = true;
                            options.Events.RaiseFailureEvents = true;
                            options.Events.RaiseSuccessEvents = true;

                            // see https://docs.duendesoftware.com/identityserver/v6/fundamentals/resources/
                            options.EmitStaticAudienceClaim = true;
                        })
                        .AddConfigurationStore(options =>
                        {

                            options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
                                sql => sql.MigrationsAssembly(migrationsAssembly));
                        })
                       .AddOperationalStore(options =>
                       {
                           options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
                               sql => sql.MigrationsAssembly(migrationsAssembly));
                       })
                       .AddAspNetIdentity<ApplicationUser>();


        return builder.Build();
    }


    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Call this only when you modify Config.cs file and when you start the application for the first time
        InitializeDatabase(app);


        // uncomment if you want to add a UI
        app.UseStaticFiles();
        app.UseRouting();

        app.UseIdentityServer();

        // uncomment if you want to add a UI
        app.UseAuthorization();
        app.MapRazorPages().RequireAuthorization();

        return app;
    }

    private static void InitializeDatabase(IApplicationBuilder app)
    {
        using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
        {
            serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

            var context = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
            context.Database.Migrate();

            #region Clients

            if (!context.Clients.Any())
            {
                foreach (var client in Config.Clients)
                {
                    context.Clients.Add(client.ToEntity());
                }
                context.SaveChanges();
            }
            else
            {
                // Check if the number of clients in the database is equal to the number of clients in the Config.Clients collection
                bool areClientsEqual = context.Clients.Count() == Config.Clients.Count();

                // Check if each client in the database has a corresponding client with the same ClientId in the Config.Clients collection
                bool areClientsMatching = context.Clients.ToList().All(dbClient => Config.Clients.Any(configClient => configClient.ClientId == dbClient.ClientId));


                if (areClientsEqual && areClientsMatching)
                {
                    // The context.Clients collection and Config.Clients collection are equal
                    // All clients in the database have corresponding clients in the Config.Clients collection
                }
                else
                {

                    // The context.Clients collection and Config.Clients collection are not equal
                    // There are missing or additional clients in either collection
                    var missingClients = Config.Clients.Where(configClient => !context.Clients.Any(dbClient => dbClient.ClientId == configClient.ClientId));

                    foreach (var missingClient in missingClients)
                    {
                        context.Clients.Add(missingClient.ToEntity());
                    }
                    context.SaveChanges();
                }
            }

            #endregion

            #region IdentityResources

            if (!context.IdentityResources.Any())
            {
                foreach (var resource in Config.IdentityResources)
                {
                    context.IdentityResources.Add(resource.ToEntity());
                }
                context.SaveChanges();
            }
            else
            {
                // Check if the number of identity resources in the database is equal to the number of identity resources in the Config.IdentityResources collection
                bool areIdentityResourcesEqual = context.IdentityResources.Count() == Config.IdentityResources.Count();

                // Check if each identity resource in the database has a corresponding resource with the same Name in the Config.IdentityResources collection
                bool areIdentityResourcesMatching = context.IdentityResources.ToList().All(dbResource => Config.IdentityResources.Any(configResource => configResource.Name == dbResource.Name));

                if (areIdentityResourcesEqual && areIdentityResourcesMatching)
                {
                    // The context.IdentityResources collection and Config.IdentityResources collection are equal
                    // All identity resources in the database have corresponding resources in the Config.IdentityResources collection
                }
                else
                {
                    // The context.IdentityResources collection and Config.IdentityResources collection are not equal
                    // There are missing or additional resources in either collection

                    var missingIdentityResources = Config.IdentityResources.Where(configResource => !context.IdentityResources.Any(dbResource => dbResource.Name == configResource.Name));

                    foreach (var missingResource in missingIdentityResources)
                    {
                        context.IdentityResources.Add(missingResource.ToEntity());
                    }

                    context.SaveChanges();
                }
            }
            #endregion

            #region ApiScopes

            if (!context.ApiScopes.Any())
            {
                foreach (var resource in Config.ApiScopes)
                {
                    context.ApiScopes.Add(resource.ToEntity());
                }
                context.SaveChanges();
            }
            else
            {
                // Check if the number of API scopes in the database is equal to the number of API scopes in the Config.ApiScopes collection
                bool areApiScopesEqual = context.ApiScopes.Count() == Config.ApiScopes.Count();

                // Check if each API scope in the database has a corresponding scope with the same Name in the Config.ApiScopes collection
                bool areApiScopesMatching = context.ApiScopes.ToList().All(dbScope => Config.ApiScopes.Any(configScope => configScope.Name == dbScope.Name));

                if (areApiScopesEqual && areApiScopesMatching)
                {
                    // The context.ApiScopes collection and Config.ApiScopes collection are equal
                    // All API scopes in the database have corresponding scopes in the Config.ApiScopes collection
                }
                else
                {
                    // The context.ApiScopes collection and Config.ApiScopes collection are not equal
                    // There are missing or additional scopes in either collection
                    var missingApiScopes = Config.ApiScopes.Where(configScope => !context.ApiScopes.Any(dbScope => dbScope.Name == configScope.Name));

                    foreach (var missingScope in missingApiScopes)
                    {
                        context.ApiScopes.Add(missingScope.ToEntity());
                    }

                    context.SaveChanges();
                }
            }
            #endregion ApiScopes
        }


        using (var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
            context.Database.Migrate();

            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var alice = userMgr.FindByNameAsync("alice").Result;
            if (alice == null)
            {
                alice = new ApplicationUser
                {
                    UserName = "alice",
                    Email = "AliceSmith@email.com",
                    EmailConfirmed = true,
                };
                var result = userMgr.CreateAsync(alice, "Pass123$").Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }

                result = userMgr.AddClaimsAsync(alice, new Claim[]{
                            new Claim(JwtClaimTypes.Name, "Alice Smith"),
                            new Claim(JwtClaimTypes.GivenName, "Alice"),
                            new Claim(JwtClaimTypes.FamilyName, "Smith"),
                            new Claim(JwtClaimTypes.WebSite, "http://alice.com"),
                        }).Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }
                Log.Debug("alice created");
            }
            else
            {
                Log.Debug("alice already exists");
            }

            var bob = userMgr.FindByNameAsync("bob").Result;
            if (bob == null)
            {
                bob = new ApplicationUser
                {
                    UserName = "bob",
                    Email = "BobSmith@email.com",
                    EmailConfirmed = true
                };
                var result = userMgr.CreateAsync(bob, "Pass123$").Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }

                result = userMgr.AddClaimsAsync(bob, new Claim[]{
                            new Claim(JwtClaimTypes.Name, "Bob Smith"),
                            new Claim(JwtClaimTypes.GivenName, "Bob"),
                            new Claim(JwtClaimTypes.FamilyName, "Smith"),
                            new Claim(JwtClaimTypes.WebSite, "http://bob.com"),
                            new Claim("location", "somewhere")
                        }).Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }
                Log.Debug("bob created");
            }
            else
            {
                Log.Debug("bob already exists");
            }
        }
    }
}

