using EPiServer;
using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Core;
using EPiServer.Find;
using EPiServer.Find.Cms;
//using EPiServer.Find.Commerce;
using EPiServer.Find.Framework.BestBets;
using EPiServer.Framework.Cache;
using EPiServer.Framework.Localization;
using EPiServer.Web.Routing;
using Foundation.Features.CatalogContent;
using Foundation.Infrastructure.Cms.Extensions;
using Mediachase.Commerce.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using Wangkanai.Detection;

namespace Foundation.Features.Search
{
    public interface ISearchViewModelFactory
    {
        SearchViewModel<TContent> Create<TContent>(TContent currentContent, string selectedFacets,
            int catlogId, FilterOptionViewModel filterOption)
            where TContent : IContent;
    }

    public class SearchViewModelFactory : ISearchViewModelFactory
    {
        private readonly ISearchService _searchService;
        private readonly LocalizationService _localizationService;
        private readonly IContentLoader _contentLoader;
        private readonly ReferenceConverter _referenceConverter;
        private readonly UrlResolver _urlResolver;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IClient _findClient;
        private readonly ISynchronizedObjectInstanceCache _synchronizedObjectInstanceCache;

        public SearchViewModelFactory(LocalizationService localizationService, ISearchService searchService,
            IContentLoader contentLoader,
            ReferenceConverter referenceConverter,
            UrlResolver urlResolver,
            IHttpContextAccessor httpContextAccessor,
            IClient findClient,
            ISynchronizedObjectInstanceCache synchronizedObjectInstanceCache)
        {
            _searchService = searchService;
            _contentLoader = contentLoader;
            _referenceConverter = referenceConverter;
            _urlResolver = urlResolver;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _findClient = findClient;
            _synchronizedObjectInstanceCache = synchronizedObjectInstanceCache;
        }

        public virtual SearchViewModel<TContent> Create<TContent>(TContent currentContent,
            string selectedFacets,
            int catalogId,
            FilterOptionViewModel filterOption)
            where TContent : IContent
        {
            var model = new SearchViewModel<TContent>(currentContent);

            if (!filterOption.Q.IsNullOrEmpty() && (filterOption.Q.StartsWith("*") || filterOption.Q.StartsWith("?")))
            {
                model.CurrentContent = currentContent;
                model.FilterOption = filterOption;
                model.HasError = true;
                model.ErrorMessage = _localizationService.GetString("/Search/BadFirstCharacter");
                model.CategoriesFilter = new CategoriesFilterViewModel();
                return model;
            }

            var results = _searchService.Search(currentContent, filterOption, selectedFacets, catalogId);

            filterOption.TotalCount = results.TotalCount;
            filterOption.FacetGroups = results.FacetGroups.ToList();

            filterOption.Sorting = _searchService.GetSortOrder().Select(x => new SelectListItem
            {
                Text = _localizationService.GetString("/Category/Sort/" + x.Name),
                Value = x.Name.ToString(),
                Selected = string.Equals(x.Name.ToString(), filterOption.Sort)
            });

            model.CurrentContent = currentContent;
            model.ProductViewModels = results?.ProductViewModels ?? new List<ProductTileViewModel>();
            model.FilterOption = filterOption;
            model.CategoriesFilter = GetCategoriesFilter(currentContent, filterOption.Q);
            model.DidYouMeans = results.DidYouMeans;
            model.Query = filterOption.Q;
            var detection = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IDetection>();
            model.IsMobile = detection.Device.Type == DeviceType.Mobile;

            return model;
        }

        private CategoriesFilterViewModel GetCategoriesFilter(IContent currentContent, string query)
        {
            var bestBets = new BestBetRepository(_synchronizedObjectInstanceCache).List().Where(i => i.PhraseCriterion.Phrase.CompareTo(query) == 0);
            //var ownStyleBestBets = bestBets.Where(i => i.BestBetSelector is CommerceBestBetSelector && i.HasOwnStyle);
            var catalogId = 0;
            var node = currentContent as NodeContent;
            if (node != null)
            {
                catalogId = node.CatalogId;
            }
            var catalog = _contentLoader.GetChildren<CatalogContentBase>(_referenceConverter.GetRootLink())
                .FirstOrDefault(x => catalogId == 0 || x.CatalogId == catalogId);

            if (catalog == null)
            {
                return new CategoriesFilterViewModel();
            }

            var viewModel = new CategoriesFilterViewModel();
            var nodes = _findClient.Search<NodeContent>()
                .Filter(x => x.ParentLink.ID.Match(catalog.ContentLink.ID))
                .FilterForVisitor()
                .GetContentResult();

            foreach (var nodeContent in nodes)
            {
                var nodeFilter = new CategoryFilter
                {
                    DisplayName = nodeContent.DisplayName,
                    Url = _urlResolver.GetUrl(nodeContent.ContentLink),
                    IsActive = currentContent != null && currentContent.ContentLink == nodeContent.ContentLink,
                    IsBestBet = false//ownStyleBestBets.Any(x => ((CommerceBestBetSelector)x.BestBetSelector).ContentLink.ID == nodeContent.ContentLink.ID)
                };
                viewModel.Categories.Add(nodeFilter);

                GetChildrenNode(currentContent, nodeContent, nodeFilter, null);
            }
            return viewModel;
        }

        private void GetChildrenNode(IContent currentContent, NodeContent node, CategoryFilter nodeFilter, IEnumerable<BestBetBase> ownStyleBestBets)
        {
            var nodeChildrenOfNode = _findClient.Search<NodeContent>()
                .Filter(x => x.ParentLink.ID.Match(node.ContentLink.ID))
                .FilterForVisitor()
                .GetContentResult();
            foreach (var nodeChildOfChild in nodeChildrenOfNode)
            {
                var nodeChildOfChildFilter = new CategoryFilter
                {
                    DisplayName = nodeChildOfChild.DisplayName,
                    Url = _urlResolver.GetUrl(nodeChildOfChild.ContentLink),
                    IsActive = currentContent != null && currentContent.ContentLink == nodeChildOfChild.ContentLink,
                    IsBestBet = false//ownStyleBestBets.Any(x => ((CommerceBestBetSelector)x.BestBetSelector).ContentLink.ID == nodeChildOfChild.ContentLink.ID)
                };

                nodeFilter.Children.Add(nodeChildOfChildFilter);
                if (nodeChildOfChildFilter.IsActive)
                {
                    nodeFilter.IsActive = nodeFilter.IsActive = true;
                }

                GetChildrenNode(currentContent, nodeChildOfChild, nodeChildOfChildFilter, ownStyleBestBets);
            }
        }
    }
}
