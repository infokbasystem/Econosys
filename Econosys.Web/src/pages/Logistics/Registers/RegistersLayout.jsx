import { Outlet } from 'react-router-dom'
import RegistersSubMenu from './RegistersSubMenu'

function RegistersLayout() {
  return (
    <div className="flex w-full flex-col px-0 py-0 md:px-[clamp(8px,15vw,10vw)]">
      <div className="mt-0 flex w-full flex-col">
        <div className="flex w-full items-stretch pb-10">
          <RegistersSubMenu />

          <div aria-hidden="true" className="mr-2 mt-2 w-px self-stretch bg-black/10" />

          <section className="w-full min-w-0 pr-2">
            <div className="pb-8 pl-10">
              <Outlet />
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}

export default RegistersLayout
