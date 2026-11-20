# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: recherche.spec.js >> Recherche de trajets (visiteur) >> le filtre par ville de départ ne garde que cette ville
- Location: tests\recherche.spec.js:13:3

# Error details

```
Error: expect(page).toHaveURL(expected) failed

Expected pattern: /departure=Sfax/
Received string:  ""

```

```
Error: apiRequestContext._wrapApiCall: Target page, context or browser has been closed
```