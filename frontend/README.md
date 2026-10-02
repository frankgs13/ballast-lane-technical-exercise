# Task Manager UI (Angular 22)

See the [root README](../README.md) for the full setup.

```bash
npm install
npm start                 # http://localhost:4200, proxies /api to http://localhost:5080
npm test -- --watch=false # Vitest unit tests
npm run build             # production build in dist/
```

## Structure and conventions

- **Standalone components, zoneless change detection, `OnPush` everywhere**: state lives in signals, so Angular only re-renders what changed.
- **`core/`** holds app-wide singletons: `AuthService` (session signal and localStorage), a functional `authInterceptor`, route guards, and problem-details error mapping.
- **`features/`** holds lazy-loaded routes. `tasks/` follows a container/presentational split:
  - `TaskListPage` (container) wires the store, dialogs and snackbars.
  - `TaskCard` (presentational) takes `input()`s and emits `output()`s, with no HTTP or state.
  - `TasksStore` is a page-scoped signal store and the only place that mutates task state.
  - `TaskApiService` is a stateless HTTP adapter.
- **Reactive forms** with client-side rules that mirror the API (the API stays the source of truth). Server validation errors are mapped back onto the matching form controls.
- **Dates**: due dates are calendar dates. `shared/date-utils.ts` avoids the classic `toISOString()` and `new Date('yyyy-MM-dd')` time-zone bugs.
