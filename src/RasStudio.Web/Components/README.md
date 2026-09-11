# UI components

```text
Components/
├── App.razor                   Application document and render mode
├── Routes.razor                Router
├── _Imports.razor              Common Razor imports
├── Layout/                    Shell, navigation, branding, themes, reconnect UI
├── Pages/                     Routable pages with their code-behind and CSS
├── Features/
│   ├── Assistant/             Chat message rendering
│   ├── Clusters/              Cluster dialogs, credentials, and dialog values
│   ├── RasEndpoints/          Endpoint editor and its values
│   ├── RasGates/              Gate editor and its values
│   └── Settings/              Settings panels
└── Shared/
    ├── Cards/                 Metric cards
    ├── Icons/                 Shared application icons
    ├── Pages/                 Page shell, title, and width
    ├── States/                Empty and loading states
    └── Tables/                Table footer and shadow catalog pagination
```

- Put components with `@page` in `Pages`; keep their `.razor.cs` and
  `.razor.css` files alongside them.
- Put non-routable domain UI in the corresponding `Features` folder. Keep each dialog's values next to the dialog.
  Cluster credentials are shared by cluster operations and infobase synchronization.
- Put domain-independent UI in `Shared`; keep application-shell concerns in
  `Layout`. Do not put new feature components in the root.
- Match namespaces to folders. Common imports belong in the root `_Imports`; imports needed by routed pages belong in
  `Pages/_Imports.razor`.
- Move CSS and JavaScript companions together with their components. Update explicit JavaScript import/asset paths
  whenever their containing folder moves.
- Application services, API adapters, and business models belong outside this UI tree. Dialog values and UI pagination
  helpers are presentation concerns.
