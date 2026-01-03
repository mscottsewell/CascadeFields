<img width="1248" height="307" alt="image" src="https://github.com/user-attachments/assets/d1d0aa60-c8b5-4585-9d45-7dff22d8587a" />

# CascadeFields for Microsoft Dataverse

CascadeFields is a Dataverse plug-in plus an XrmToolBox Configurator that helps keep related records in sync by cascading field values from parent records to their related child records.
<img width="1349" alt="image" src="https://github.com/user-attachments/assets/6c0cd72e-80f4-4b9d-833d-629f343cbd55" />

## Why CascadeFields?

Keeping related records consistent in Dataverse often means manual updates, complex Power Automate flows, or custom plug-in development. CascadeFields is intended to give admins and teams a fast, repeatable way to keep child records aligned with parent data while remaining solution-aware and easy to manage.

> **Doesn't this result in data duplication?**
Sure, yes. But there are cases where duplicating the data makes it easier for users to search/filter within Dataverse.
A simplified example is where you have the hierarchy of Account-Contact records. If a record is attached to the contact record via a lookup it's readily visible in the child's related records, but if you go to the Account record (the parent of the contact) this child of a child record is not in a related view. - Now consider if the records are arrainged in a multiple level heirarcy, keeping a link to the parent records on the ultimate child record goes a long way toward making the records easily discoverable by an end-user. It's an *option* for you when you want to add these extra shortcuts/relationships just to make the app easier to use through the front-end. - If it helps, great! If you want to solve it another way, also great! :)

## What it does

- Cascades changes from a **parent** record to matching **child** records (typically async for performance)
- Populates mapped fields when a **child** record is created and linked to a parent
- Updates mapped fields when a **child** record is re-linked to a different parent
- Supports trigger fields and optional filters to control when and what is updated

## Components

- **CascadeFields Configurator**: XrmToolBox UI used to build, validate, and publish configurations
- **CascadeFields Plugin**: Dataverse plug-in that executes the configured cascade behavior

## Key features

- No-code configuration from within XrmToolBox
- Trigger fields (control which parent changes cause cascades)
- Optional filter criteria (control which child rows are updated)
- Supports parent update cascades and child create/relink scenarios
- Designed for performance (asynchronous parent updates where appropriate)
- Diagnostics via Dataverse plug-in trace logs (when enabled at the environment level)

## Docs Index

- Admin usage (XrmToolBox): [QUICKSTART.md](QUICKSTART.md)
- Configuration JSON reference: [CONFIGURATION.md](CONFIGURATION.md)
- Build, packaging, manual registration, contributing: [PRODEV.md](PRODEV.md)
- Examples: [Examples](Examples/)

## License

Licensed under the [MIT License](LICENSE).
