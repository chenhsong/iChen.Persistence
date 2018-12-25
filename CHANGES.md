Release 4.2
===========

Enhancements
------------

- Add `UpdateAsync` method for shared caches. This method
  will simply update a collection's individual properties
  instead of replacing the entire set, as in `SaveAsync`.
  Existing properties are left intact if not modified.

- Azure Table Storage driver is now more responsive.

- A `UniqueID` field is added to the `CycleData` record class,
  which may be provided by the server allowing tracking of data
  between individual cycles.

- A `Links` table under Azure Table Storage is added containing
  unique ID key mappings.

Breaking Changes
----------------

- Azure Table Storage driver now accepts both HTTP and HTTPS
  protocols, selected via a parameter to the constructor.
