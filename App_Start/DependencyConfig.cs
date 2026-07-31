using ERPaperless.Abstractions.Application;
using ERPaperless.Abstractions.Infrastructure;
using ERPaperless.Abstractions.Security;
using ERPaperless.Application.Services;
using ERPaperless.Infrastructure.Infrastructure;
using ERPaperless.Infrastructure.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Dependencies;
using System.Web.Mvc;

namespace ERPaperless
{
    public static class DependencyConfig
    {
        public static CompositeDependencyResolver Resolver { get; private set; }

        public static void Register()
        {
            var registrations = new Dictionary<Type, Func<object>>
            {
                { typeof(IDbConnectionFactory), () => new DbConnectionFactory() },
                { typeof(IDbContextFactory), () => new DbContextFactory() },
                { typeof(ICurrentUserContext), () => new CurrentUserContext() },
                { typeof(IPatientWorkspaceService), () => new PatientWorkspaceService() },
                { typeof(IErFormService), () => new ErFormService() },
                { typeof(IBillingService), () => new BillingService() },
                { typeof(IPharmacyService), () => new PharmacyService() },
                { typeof(IReportsPortalService), () => new ReportsPortalService() }
            };

            Resolver = new CompositeDependencyResolver(registrations);
            DependencyResolver.SetResolver(Resolver);
        }

        public static void ConfigureWebApi(HttpConfiguration config)
        {
            if (Resolver != null)
                config.DependencyResolver = Resolver;
        }
    }

    public sealed class CompositeDependencyResolver :
        System.Web.Mvc.IDependencyResolver,
        System.Web.Http.Dependencies.IDependencyResolver
    {
        private readonly IDictionary<Type, Func<object>> _registrations;

        public CompositeDependencyResolver(IDictionary<Type, Func<object>> registrations)
        {
            _registrations = registrations ?? new Dictionary<Type, Func<object>>();
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) return null;

            if (_registrations.TryGetValue(serviceType, out var factory))
                return factory();

            if (serviceType.IsAbstract || serviceType.IsInterface)
                return null;

            return CreateConcrete(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            var service = GetService(serviceType);
            return service == null ? Enumerable.Empty<object>() : new[] { service };
        }

        public IDependencyScope BeginScope()
        {
            return this;
        }

        public void Dispose()
        {
        }

        private object CreateConcrete(Type concreteType)
        {
            var constructors = concreteType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToList();

            foreach (var constructor in constructors)
            {
                var args = new List<object>();
                var canBuild = true;
                foreach (var parameter in constructor.GetParameters())
                {
                    var dep = GetService(parameter.ParameterType);
                    if (dep == null)
                    {
                        canBuild = false;
                        break;
                    }
                    args.Add(dep);
                }

                if (canBuild)
                    return Activator.CreateInstance(concreteType, args.ToArray());
            }

            return Activator.CreateInstance(concreteType);
        }
    }
}
