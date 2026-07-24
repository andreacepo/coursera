# Step 4: Test and Validate Fixes

## Test scope
The debugged API was tested against edge cases for validation, non-existent IDs, duplicate data, and query guardrails.

Base URL used:
- http://localhost:5004

## Edge-case test results
1. GET /users?page=1&pageSize=20
- PASS
- Returned baseline user list.

2. POST /users with valid payload
- PASS
- User created successfully (201) and ID returned.

3. POST /users with duplicate email
- PASS
- Returned 409 Conflict.

4. POST /users with empty name
- PASS
- Returned 400 Bad Request.

5. POST /users with invalid email format
- PASS
- Returned 400 Bad Request.

6. POST /users with name length > 100
- PASS
- Returned 400 Bad Request.

7. GET /users/{id} for non-existent ID
- PASS
- Returned 404 Not Found.

8. PUT /users/{id} for non-existent ID
- PASS
- Returned 404 Not Found.

9. DELETE /users/{id} for non-existent ID
- PASS
- Returned 404 Not Found.

10. GET /users?page=-1&pageSize=20
- PASS
- Returned 400 Bad Request (invalid page).

11. GET /users?page=1&pageSize=201
- PASS
- Returned 400 Bad Request (pageSize guard).

12. GET /users with search filter
- PASS
- Returned filtered subset as expected.

13. Cleanup delete of created test user
- PASS

## How Microsoft Copilot helped identify and resolve issues
1. Validation hardening
- Suggested stronger input checks beyond null/empty values.
- Implemented name length bounds and stricter email validation.

2. Exception safety
- Suggested explicit try-catch around endpoint handlers.
- Added local error handling plus centralized exception middleware response behavior.

3. Not-found and conflict handling
- Suggested explicit status responses for known failure cases.
- Implemented consistent 404 for missing users and 409 for duplicate email conflicts.

4. Performance-oriented improvements
- Suggested reducing expensive list scans by introducing indexed lookup structures.
- Updated in-memory store logic to use concurrent dictionaries and support paging/search on GET /users.

5. Verification workflow
- Suggested repeatable edge-case test scenarios and iterative validation after each fix.
- Result: all targeted regression and edge-case checks passed.

## Conclusion
The API fixes are validated for the targeted bug categories: invalid input handling, missing-resource handling, exception resilience, and basic query/performance guardrails.
