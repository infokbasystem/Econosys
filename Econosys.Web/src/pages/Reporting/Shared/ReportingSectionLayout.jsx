import { Outlet } from 'react-router-dom'
import LeftMenu from '../../../components/LeftMenu'

function ReportingSectionLayout({ groups }) {
  return (
    <div className="flex w-full flex-col px-0 py-0 md:px-[clamp(2px,2vw,2vw)]">
      <div className="mt-0 flex w-full flex-col">
        <div className="flex w-full items-stretch pb-10">
          <aside className="mt-4 w-60 shrink-0 self-stretch pr-6">
            <div className="sticky top-[50px] max-h-[calc(100dvh-120px)] overflow-y-auto mb-20">
              <LeftMenu groups={groups} showGroupLabels />
            </div>
          </aside>

          <div aria-hidden="true" className="mr-0 mt-2 w-px self-stretch bg-black/10" />

          <section className="w-full min-w-0 pr-0">
            <div className="pb-8 pl-0">
              <Outlet />
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}

export default ReportingSectionLayout
