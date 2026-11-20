const { test, expect } = require('@playwright/test');

test.describe('Recherche de trajets (visiteur)', () => {

  test('la page liste les trajets avec pagination', async ({ page }) => {
    await page.goto('/Trip');

    await expect(page.getByRole('heading', { name: 'Trajets disponibles' })).toBeVisible();
    await expect(page.locator('.trip-card').first()).toBeVisible();
    await expect(page.getByText(/Page 1 \/ \d+/)).toBeVisible();
  });

  test('le filtre par ville de départ ne garde que cette ville', async ({ page }) => {
    await page.goto('/Trip');

    await page.getByLabel('Départ', { exact: true }).fill('Sfax');
    await page.getByRole('button', { name: 'Rechercher' }).click();

    await expect(page).toHaveURL(/departure=Sfax/);

    const villesDepart = await page
      .locator('.trip-card .route-stack .route-row:first-child span:not(.route-dot)')
      .allTextContents();

    expect(villesDepart.length).toBeGreaterThan(0);
    for (const ville of villesDepart) {
      expect(ville.trim()).toBe('Sfax');
    }
  });

  test('le bouton Suivant mène à la page 2', async ({ page }) => {
    await page.goto('/Trip');

    await page.getByRole('link', { name: /Suivant/ }).click();

    await expect(page).toHaveURL(/page=2/);
    await expect(page.getByText(/Page 2 \/ \d+/)).toBeVisible();
  });
});