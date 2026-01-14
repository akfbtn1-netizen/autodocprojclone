import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type {
  SearchState,
  SearchFilters,
  SearchResultItem,
  FollowUpSuggestion,
  SearchMetadata,
  RoutingPath,
} from '../types/search';

interface SearchActions {
  setQuery: (query: string) => void;
  setResults: (results: SearchResultItem[]) => void;
  setCurrentQueryId: (queryId: string | null) => void;
  setRoutingPath: (path: RoutingPath | null) => void;
  setIsLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  setFilters: (filters: Partial<SearchFilters>) => void;
  resetFilters: () => void;
  setSuggestions: (suggestions: string[]) => void;
  setFollowUpSuggestions: (suggestions: FollowUpSuggestion[]) => void;
  setMetadata: (metadata: SearchMetadata | null) => void;
  clearSearch: () => void;
  togglePiiFilter: () => void;
  addDatabaseFilter: (database: string) => void;
  removeDatabaseFilter: (database: string) => void;
  addObjectTypeFilter: (objectType: string) => void;
  removeObjectTypeFilter: (objectType: string) => void;
  addCategoryFilter: (category: string) => void;
  removeCategoryFilter: (category: string) => void;
}

const defaultFilters: SearchFilters = {
  databases: [],
  objectTypes: [],
  categories: [],
  showPiiOnly: false,
};

const initialState: SearchState = {
  query: '',
  results: [],
  currentQueryId: null,
  routingPath: null,
  isLoading: false,
  error: null,
  filters: defaultFilters,
  suggestions: [],
  followUpSuggestions: [],
  metadata: null,
};

export const useSearchStore = create<SearchState & SearchActions>()(
  persist(
    (set) => ({
      ...initialState,

      setQuery: (query) => set({ query }),

      setResults: (results) => set({ results }),

      setCurrentQueryId: (queryId) => set({ currentQueryId: queryId }),

      setRoutingPath: (path) => set({ routingPath: path }),

      setIsLoading: (loading) => set({ isLoading: loading }),

      setError: (error) => set({ error }),

      setFilters: (filters) =>
        set((state) => ({
          filters: { ...state.filters, ...filters },
        })),

      resetFilters: () => set({ filters: defaultFilters }),

      setSuggestions: (suggestions) => set({ suggestions }),

      setFollowUpSuggestions: (suggestions) =>
        set({ followUpSuggestions: suggestions }),

      setMetadata: (metadata) => set({ metadata }),

      clearSearch: () =>
        set({
          query: '',
          results: [],
          currentQueryId: null,
          routingPath: null,
          error: null,
          suggestions: [],
          followUpSuggestions: [],
          metadata: null,
        }),

      togglePiiFilter: () =>
        set((state) => ({
          filters: {
            ...state.filters,
            showPiiOnly: !state.filters.showPiiOnly,
          },
        })),

      addDatabaseFilter: (database) =>
        set((state) => ({
          filters: {
            ...state.filters,
            databases: [...state.filters.databases, database],
          },
        })),

      removeDatabaseFilter: (database) =>
        set((state) => ({
          filters: {
            ...state.filters,
            databases: state.filters.databases.filter((d) => d !== database),
          },
        })),

      addObjectTypeFilter: (objectType) =>
        set((state) => ({
          filters: {
            ...state.filters,
            objectTypes: [...state.filters.objectTypes, objectType],
          },
        })),

      removeObjectTypeFilter: (objectType) =>
        set((state) => ({
          filters: {
            ...state.filters,
            objectTypes: state.filters.objectTypes.filter(
              (t) => t !== objectType
            ),
          },
        })),

      addCategoryFilter: (category) =>
        set((state) => ({
          filters: {
            ...state.filters,
            categories: [...state.filters.categories, category],
          },
        })),

      removeCategoryFilter: (category) =>
        set((state) => ({
          filters: {
            ...state.filters,
            categories: state.filters.categories.filter((c) => c !== category),
          },
        })),
    }),
    {
      name: 'search-store',
      partialize: (state) => ({
        filters: state.filters,
      }),
    }
  )
);

export default useSearchStore;
