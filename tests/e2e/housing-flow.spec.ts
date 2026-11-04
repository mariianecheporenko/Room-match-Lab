import { expect, test } from '@playwright/test';

test('approved landlord creates a housing listing', async ({ page }) => {
  const email = process.env.E2E_EMAIL;
  const password = process.env.E2E_PASSWORD;
  expect(email, 'Set E2E_EMAIL to an approved landlord account').toBeTruthy();
  expect(password, 'Set E2E_PASSWORD to that account password').toBeTruthy();

  await page.goto('/HousingsMvc');
  await expect(page.getByRole('heading', { name: 'Available housings' })).toBeVisible();
  await expect(page.locator('tbody td.fw-semibold').first()).toBeVisible();

  // Creating listings is restricted to approved landlords and admins.
  await page.goto('/ProfilesMVC/Create');
  await page.locator('#loginEmail').fill(email!);
  await page.locator('#loginPassword').fill(password!);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/$/);

  await page.goto('/HousingsMvc');
  const createHousingLink = page.getByRole('link', { name: 'Create New Housing' });
  await expect(createHousingLink, 'Sign in with an approved landlord or admin account').toBeVisible();
  await createHousingLink.click();
  await expect(page.getByRole('heading', { name: 'Create housing' })).toBeVisible();

  const title = `Playwright housing ${Date.now()}`;
  await page.locator('[name="Title"]').fill(title);
  await page.locator('[name="PricePerMonth"]').fill('12345');
  // The MVC form captures an address in separate City, Street and BuildingNumber fields.
  await page.locator('[name="City"]').fill('Kyiv');
  await page.locator('[name="Street"]').fill('Playwright Street');
  await page.locator('[name="BuildingNumber"]').fill('42');
  await page.locator('[name="Latitude"]').fill('50');
  await page.locator('[name="Longitude"]').fill('30');
  await page.locator('[name="ImageUrl"]').fill('https://example.com/playwright-housing.jpg');

  await page.getByRole('button', { name: 'Create housing' }).click();

  const catalogHeading = page.getByRole('heading', { name: 'Available housings' });
  const pageResponse = await page.locator('body').innerText();
  await expect(
    catalogHeading,
    `Housing creation did not return to the catalog. Page response:\n${pageResponse}`,
  ).toBeVisible();
  await expect(page.getByRole('cell', { name: title, exact: true })).toBeVisible();
});
