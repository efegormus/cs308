# SCRUM-42 and seed-data tests — draft test plan

These are **test cases to implement**, not executed xUnit tests. Adapt to the final models and EF Core setup.

## Unit test ideas (no database needed)

1. Seed input contains exactly two categories.
2. Category codes are unique.
3. Category names are nonempty.
4. Seed input contains ten products.
5. Every product has a nonempty name.
6. Every product has a nonempty model.
7. Every product has a nonempty serial number.
8. Serial numbers are unique in the seed data.
9. Every product has a nonempty description.
10. Every product has a positive price.
11. Every product has a nonnegative stock quantity.
12. Every product has a warranty status.
13. Every product has distributor information.
14. Every product category code exists in the category list.
15. Every product has a unique seed key.
16. All sample costs are nonnegative and not greater than their prices.

## Integration test ideas (require EF Core and a test database)

17. First seed run saves both categories.
18. First seed run saves all ten products.
19. Products persist with correct prices and stock quantities.
20. Products have valid category relationships.
21. Re-running the seed does not add duplicate categories.
22. Re-running the seed does not add duplicate products.
23. A fresh database can be seeded successfully.
24. Products are retrievable by category through the data access layer.
25. Seed data survives a fresh database context read.

**Note:** 17–25 are integration tests, not unit tests. The course asks for 25 *new unit test cases per demo across the team's development activities*; this list should not be misrepresented as 25 finished unit tests.

## Definition of done

- Seed data matches the actual Product/Category C# model property names and types.
- Seed operation creates required initial records in MySQL.
- Repeated runs do not create unintended duplicates.
- Automated tests are written and run; failures are fixed.
- Changes are reviewed and committed to the SCRUM-42 feature branch.
