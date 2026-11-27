# Test Coverage

- Test observable behavior and domain rules, not private implementation details.
- Use Arrange, Act, Assert. Give tests names that describe the expected result.
- Cover successful flows, rejected actions, invalid transitions, and boundary conditions.
- Test authorization for each user role and each protected operation.
- Test transactional invariants with the selected relational database provider, not mocks alone.
- Keep unit tests isolated from network access and external services.
- Run focused tests first, then the full suite.
- If a test fails, stop and report the failure. Do not change code until the user approves a fix.
