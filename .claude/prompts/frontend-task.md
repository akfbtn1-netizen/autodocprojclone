# Frontend Development Task

## Expert Mode: Senior Frontend Developer
Reference: senior-frontend skill patterns

## Task Description
[DESCRIBE YOUR FRONTEND TASK HERE]

## Apply These Patterns

### TypeScript Best Practices
- Explicit type annotations for all props and state
- Use interfaces for complex objects
- Leverage union types and discriminated unions
- Avoid `any` - use `unknown` when type is truly unknown

### React Patterns
- Functional components with hooks
- Custom hooks for reusable logic
- Component composition over prop drilling
- Proper dependency arrays in useEffect
- Memoization (useMemo, useCallback) only when needed

### Performance
- Code splitting with lazy loading
- Optimize bundle size
- Avoid unnecessary re-renders
- Use React.memo strategically

### Styling (Tailwind CSS)
- Utility-first approach
- Responsive design with mobile-first
- Consistent spacing and color system
- Extract repeated patterns to components

### State Management
- Local state with useState for simple cases
- Context API for app-wide state
- Consider external libraries (Zustand, Jotai) for complex state

## Project Context
- **Location**: `src/WebApi/ClientApp` or frontend directory
- **Framework**: React 18+ with TypeScript
- **Styling**: Tailwind CSS
- **Build Tool**: Vite or Webpack
- **Testing**: Jest + React Testing Library

## Success Criteria
- [ ] TypeScript compiles with no errors
- [ ] Component is properly typed
- [ ] Responsive design works on all breakpoints
- [ ] Accessible (ARIA labels, keyboard navigation)
- [ ] No console errors or warnings
- [ ] Performance optimized (no unnecessary re-renders)

## Next Steps After Implementation
1. Test component in isolation
2. Integrate with existing app
3. Add unit tests
4. Verify responsive design
5. Check accessibility with screen reader
