export async function emulateTimezone(page, timezoneId) {
  const session = await page.context().newCDPSession(page);
  await session.send("Emulation.setTimezoneOverride", { timezoneId });
  page.__heimdallTimezoneSession = session;
}
