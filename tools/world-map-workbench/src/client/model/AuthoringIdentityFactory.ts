import type { GeographyDocument } from "../../shared/types.js";

export interface ProvinceCityIdentity {
  provinceId: string;
  locationId: string;
  layoutId: string;
}

export type AuthoringEntropy = () => string;

const DEFAULT_MAX_ATTEMPTS = 128;

function defaultEntropy(): string {
  return crypto.randomUUID();
}

function normalizedToken(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, "").slice(0, 12);
}

/** Generates map-local stable identities without deriving them from mutable names or geometry. */
export class AuthoringIdentityFactory {
  private readonly usedIds: Set<string>;

  public constructor(
    geography: Pick<GeographyDocument, "provinces" | "strategicLocations">,
    private readonly entropy: AuthoringEntropy = defaultEntropy,
    private readonly maxAttempts = DEFAULT_MAX_ATTEMPTS,
  ) {
    this.usedIds = new Set([
      ...geography.provinces.flatMap((province) => [province.provinceId, province.layoutId]),
      ...geography.strategicLocations.features.map((location) => location.properties.locationId),
    ]);
  }

  public createProvinceCityIdentity(): ProvinceCityIdentity {
    return {
      provinceId: this.next("province"),
      locationId: this.next("location"),
      layoutId: this.next("layout"),
    };
  }

  public createLocationId(): string {
    return this.next("location");
  }

  private next(prefix: "province" | "location" | "layout"): string {
    for (let attempt = 0; attempt < this.maxAttempts; attempt += 1) {
      const token = normalizedToken(this.entropy());
      if (token === "") continue;
      const candidate = `${prefix}_${token}`;
      if (this.usedIds.has(candidate)) continue;
      this.usedIds.add(candidate);
      return candidate;
    }
    throw new Error(`Unable to generate a unique ${prefix} identity after ${this.maxAttempts} attempts`);
  }
}
