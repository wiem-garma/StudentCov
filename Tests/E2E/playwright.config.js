// @ts-check
const { defineConfig, devices } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  timeout: 30000,
  workers: 1,
  retries: 0,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../Rapports/e2e/rapport-html', open: 'never' }],
  ],
  outputDir: '../Rapports/e2e/artefacts',
  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:5284',
    locale: 'fr-FR',
    video: 'on',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  // EQ-COMP-01 : les mêmes tests sur 3 moteurs de navigateur
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox',  use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit',   use: { ...devices['Desktop Safari'] } },
  ],
});