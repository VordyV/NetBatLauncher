from __future__ import annotations
from typing import TYPE_CHECKING
if TYPE_CHECKING: from tessellock.core import Tessellock
from fastapi import Request, Depends, HTTPException
from typing import Annotated

class ViewContext:

	def __init__(self, request: Request):
		self.request = request
		self.core: Tessellock = request.app.state.core
		self.p = self.core.p
		self.account_id: int | None = None
		self.session_id: int | None = None

# unchecked view context. It does not perform authorization
def uvctx(request: Request) -> ViewContext:
	return ViewContext(request)

async def vctx(request: Request) -> ViewContext:
	vc = ViewContext(request)
	header = vc.request.headers.get("Authorization", "").strip()
	if not header.startswith("Bearer "):
		raise HTTPException(status_code=401, detail="Access token is not specified")
	token = header.removeprefix("Bearer ").strip()
	if not token:
		raise HTTPException(status_code=401, detail="Access token is not specified")
	acc_sess = await vc.core.account_session_service.authenticate(token)
	vc.account_id = acc_sess[0]
	vc.session_id = acc_sess[1]
	return vc

def permission(code: str):

	async def func(ctx: VC):
		has_perm = await ctx.core.permission_service.has_permission(ctx.account_id, code)
		if not has_perm: raise HTTPException(status_code=404, detail="No permission to perform this action")

	return func


# unchecked view context
UVC = Annotated[ViewContext, Depends(uvctx)]
# checked view context
VC = Annotated[ViewContext, Depends(vctx)]