// UC2 step 6/7: no CitySite selection yet -> initial setup; otherwise -> dashboard.
export function destinationFor(hasCitySiteSelection: boolean | null): string {
  return hasCitySiteSelection ? '/dashboard' : '/setup';
}
