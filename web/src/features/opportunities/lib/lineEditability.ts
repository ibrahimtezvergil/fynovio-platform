import type { Opportunity } from '../schema'

/**
 * INTEGRATION GAP (Phase 2.5B plan G4): `GET /opportunities/{id}/actions` has no `canAddLine`/`canCancelLine`,
 * and the lifecycle rule for lines lives only in the domain (lines are added in Draft; cancelled until terminal).
 * This is the ONE place the browser reads the lifecycle to decide whether to OFFER those two controls. It is a
 * presentation hint, not authorization: the backend re-authorizes and re-validates every call. Replace it with
 * the backend projection the moment the DTO grows those flags.
 */
export function lineEditability(opportunity: Pick<Opportunity, 'status'>): { canOfferAdd: boolean; canOfferCancel: boolean } {
  return {
    canOfferAdd: opportunity.status === 'Draft',
    canOfferCancel: opportunity.status === 'Draft' || opportunity.status === 'Open',
  }
}
