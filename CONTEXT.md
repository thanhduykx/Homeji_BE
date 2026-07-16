# Homeji

Homeji connects students, renters, landlords, rental listings, roommate matches, and nearby second-hand items in one housing marketplace.

## Language

**Rental Post**:
A listing for a vacant room or an occupied room seeking a roommate.
_Avoid_: Post, room post, listing post

**Rental Review**:
A renter's rating and optional public comment about one rental post. A reviewer has at most one current review per rental post.
_Avoid_: Comment, feedback

**Roommate Invitation**:
A request between two users who saved the same rental post; acceptance represents double opt-in.
_Avoid_: Match request, friend request

**Roommate Match**:
The relationship created only after a roommate invitation is accepted, which unlocks private conversation.
_Avoid_: Candidate, invitation

**Marketplace Order**:
A single checkout between one buyer and one seller that can contain one or more item lines. Its status changes and cancellation apply to every line together.
_Avoid_: Item order, per-item order

**Marketplace Listing Type**:
The mutually exclusive kind selected when publishing a marketplace item. Food appears only in Food; household and second-hand items appear only in Marketplace.
_Avoid_: Inferring the type from the title, showing one item in both sections

**Marketplace Order Line**:
One purchased marketplace item and quantity inside a Marketplace Order.
_Avoid_: Separate order, product order

**Marketplace Gross Amount**:
The full amount paid by the buyer for a Marketplace Order before any fees are withheld.
_Avoid_: Seller revenue, seller payout

**Platform Fee**:
The commission Homeji withholds from the Marketplace Gross Amount before any money reaches the seller wallet.
_Avoid_: Wallet debit, seller withdrawal

**Seller Net Amount**:
The amount credited to the seller wallet after all applicable order fees have been withheld from the Marketplace Gross Amount.
_Avoid_: Gross revenue, order total

**Marketplace Order Refund**:
A single wallet credit equal to the Marketplace Gross Amount of the whole checkout, even when that checkout contains multiple Marketplace Order Lines.
_Avoid_: Per-item refund, line refund
